// Cosmic Bloom — one engine for all 60 quests. Quest identity, structure,
// triggers, and rewards come exclusively from quests.json; this file only
// implements the step-type semantics shared by every quest.
//
// Activation model (DECISION, overnight run):
//  * q_arrival auto-starts on a fresh save.
//  * Completing a quest auto-activates its `unlocks` successor (the 47-quest
//    main chain).
//  * Side quests (no inbound `unlocks`) activate the first time the player
//    talks to their giver NPC.
//
// Step support: step types whose backing systems are in scope run on real
// events/polling (see QUEST_GAP_REPORT.md for the Phase-2 build rationale
// behind each). Types still requiring out-of-scope systems (challenge runs,
// trading) surface in the quest log with an explicit "[skip — system pending]"
// affordance rather than silently auto-completing.
// List: challenge_run, trade_visitors — both Act-4 optional side quests,
// neither blocks the 47-quest main chain (LEAVE STUBBED per Phase-3 ruling).
//
// Phase 2 additions (2026-07-05, all reuse existing verbs/events, no new
// systems — see BUILD_LOG "Phase-2 Spec / Phase 2"):
//   catch_fish   — LandmarkController.TryFish at a restored "fishing" landmark
//                  fires GameEvents.FishCaught; Bump("catch_fish") here.
//   collect      — now spawns real world pickups (World.PickupSpawner) for
//                  non-animal items too; ItemGranted already drove this.
//   explore      — proximity poll against ExploreTargets (small code-side
//                  target-position table — data has no coordinates for
//                  crystal_cave/crystal_cave_depths/crash_site).
//   wait_for     — meteor_shower fires on every TimeManager.OnNightfall
//                  (Core.MeteorShowerService); Bump("wait_for") here.
//   deposit_seeds— LandmarkController.TryDepositSeeds at the restored
//                  "lore_journal_2" landmark calls QuestEngine.TryDepositSeeds.
//   deliver      — quest activation grants Save.heldItems[item]; talking to
//                  step.target while holding it completes + consumes it.
//   craft        — SIMPLIFIED (no recipe system, user-approved): a small
//                  code-side CraftInputs table maps festival_lantern <- star_honey;
//                  each ItemGranted(star_honey) bumps the matching craft step.
//
// Phase 3 additions (2026-07-05, collapsed phase — both reuse Phase-2 infra,
// user-approved rulings in QUEST_GAP_REPORT.md / CLAUDE.md):
//   witness_event — counts firings of the SAME GameEvents.MeteorShower that
//                   wait_for completes on (q_comet_chaser ×5). No new events.
//   host_festival — SIMPLIFIED per q_festival's own data precedent: talking
//                   to FestivalTalkQuota distinct NPCs of one Sacred Garden
//                   (the four act-3 regions) within a single in-game day
//                   (TimeManager.DayNumber) hosts that garden's festival;
//                   step.count distinct gardens complete the step
//                   (q_festival_circuit ×4). The step data carries no harvest
//                   quota, so none is imposed.
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.Core
{
    public class QuestEngine : MonoBehaviour
    {
        public GameState State { get; private set; }

        public event Action<QuestDef> OnQuestStarted;
        public event Action<QuestDef> OnQuestCompleted;
        public event Action OnProgressChanged;

        // step types that cannot progress without out-of-scope systems
        // (q_challenge_sprout / q_galactic_traders — LEAVE STUBBED per the
        // Phase-3 ruling; witness_event/host_festival built in Phase 3)
        static readonly HashSet<string> PendingSystemSteps = new()
        {
            "challenge_run", "trade_visitors",
        };

        // craft simplification: output item -> the raw material that "becomes" it
        // (no recipe system; see QUEST_GAP_REPORT.md q_lantern_light)
        static readonly Dictionary<string, string> CraftInputs = new()
        {
            { "festival_lantern", "star_honey" },
        };

        // in-memory distinct-tracking for talk_to / sell_distinct style steps
        // (host_festival reuses it for its hosted-gardens set)
        readonly Dictionary<string, HashSet<string>> _distinct = new();

        // host_festival: q_festival's own talk_to step lists 4 NPCs — the
        // per-garden quota mirrors that data precedent
        const int FestivalTalkQuota = 4;

        // host_festival: per-quest, per-garden NPCs talked to today; a day
        // rollover restarts partial tallies ("within one in-game day")
        class FestivalTally { public int day; public readonly Dictionary<string, HashSet<string>> talked = new(); }
        readonly Dictionary<string, FestivalTally> _festival = new();

        static int Today() => TimeManager.Instance != null ? TimeManager.Instance.DayNumber : 1;

        float _pollTimer;

        public void Bind(GameState state)
        {
            State = state;
            GameDatabase.LoadAll();

            GameEvents.OnPlotTilled += r => Bump("clear_plots", s => true);
            GameEvents.OnCropPlanted += (crop, r) => Bump("plant_crop", s => s.crop == null || s.crop == crop);
            GameEvents.OnCropHarvested += (crop, r) => OnHarvest(crop);
            GameEvents.OnTalkedTo += OnTalkedTo;
            GameEvents.OnLandmarkRestored += (lm, r) => Bump("restore_landmark", s => s.landmark == lm, complete: true);
            GameEvents.OnRegionUnlocked += reg => Bump("unlock_region", s => s.region == reg, complete: true);
            GameEvents.OnAnimalBought += a => Bump("buy_animal", s => s.animal == null || s.animal == a);
            GameEvents.OnItemGranted += item => { Bump("collect", s => s.item == item); HandleCraftInput(item); };
            GameEvents.OnFishCaught += () => Bump("catch_fish", s => true);
            GameEvents.OnMeteorShower += () =>
            {
                Bump("wait_for", s => s.@event == "meteor_shower", complete: true);
                Bump("witness_event", s => s.@event == "meteor_shower");
            };

            if (!State.Save.questsCompleted.Contains("q_arrival") && ActiveQuests().Count == 0)
                StartQuest("q_arrival");
        }

        void Update()
        {
            _pollTimer += Time.deltaTime;
            if (_pollTimer < 1f) return;
            _pollTimer = 0f;
            PollThresholdSteps();
        }

        // ── quest state accessors (save-backed) ──────────────────────────────

        public List<string> ActiveQuests() => State.Save.activeQuests;

        public bool IsActive(string id) => State.Save.activeQuests.Contains(id);
        public bool IsCompleted(string id) => State.Save.questsCompleted.Contains(id);

        public int StepIndex(string questId) =>
            State.Save.questStepIndex.TryGetValue(questId, out var i) ? i : 0;

        public int StepProgress(string questId) =>
            State.Save.questStepProgress.TryGetValue(questId, out var p) ? p : 0;

        public QuestStep CurrentStep(string questId)
        {
            var def = GameDatabase.Quest(questId);
            int idx = StepIndex(questId);
            return def != null && idx < def.steps.Count ? def.steps[idx] : null;
        }

        public static bool IsPendingSystemStep(QuestStep s) =>
            s != null && PendingSystemSteps.Contains(s.type);

        /// <summary>Animal-produce items are collected via AnimalController; everything
        /// else spawns as a real world pickup (World.PickupSpawner).</summary>
        public static bool IsAnimalProduce(string item) =>
            item != null && GameDatabase.Animals.Any(a => a.produce == item);

        /// <summary>True if any active quest's current step is of the given type
        /// (used by World controllers to decide whether to attempt a step-specific action).</summary>
        public bool HasActiveStepOfType(string type) =>
            ActiveQuests().Any(id => CurrentStep(id)?.type == type);

        // ── activation / completion ───────────────────────────────────────────

        public void StartQuest(string id)
        {
            var def = GameDatabase.Quest(id);
            if (def == null || IsActive(id) || IsCompleted(id)) return;

            State.Save.activeQuests.Add(id);
            State.Save.questStepIndex[id] = 0;
            State.Save.questStepProgress[id] = 0;
            if (GameDatabase.Quest(State.Save.activeQuest ?? "") == null || IsCompleted(State.Save.activeQuest))
                State.Save.activeQuest = id;
            State.MarkDirty();
            Debug.Log($"[Quest] started {id}: {def.title}");
            OnQuestStarted?.Invoke(def);

            // cutscene steps present as dialogue immediately
            TryPresentCutscene(id);
            TryGrantDeliverItem(id);
        }

        void TryPresentCutscene(string questId)
        {
            var step = CurrentStep(questId);
            var def = GameDatabase.Quest(questId);
            if (step == null || step.type != "cutscene") return;
            string speaker = def.giver != null ? (GameDatabase.Npc(def.giver)?.name ?? "???") : "Mysterious Guide";
            UI.DialogueUI.Show(speaker, def.summary, () => AdvanceStep(questId));
        }

        /// <summary>deliver steps hand the item over the moment they become current
        /// (quest activation grants it — no separate pickup/craft for the crate itself).</summary>
        void TryGrantDeliverItem(string questId)
        {
            var step = CurrentStep(questId);
            if (step == null || step.type != "deliver" || step.item == null) return;
            if (State.Save.heldItems.Contains(step.item)) return;
            State.Save.heldItems.Add(step.item);
            State.MarkDirty();
            Debug.Log($"[Quest] {questId} granted held item {step.item} for delivery to {step.target}");
        }

        /// <summary>Explicit skip for steps whose systems aren't built (quest log button).</summary>
        public void DebugSkipStep(string questId)
        {
            var step = CurrentStep(questId);
            if (step == null || !IsPendingSystemStep(step)) return;
            Debug.LogWarning($"[Quest] SKIPPED pending-system step {step.type} in {questId}");
            AdvanceStep(questId);
        }

        void AdvanceStep(string questId)
        {
            var def = GameDatabase.Quest(questId);
            int idx = StepIndex(questId) + 1;
            State.Save.questStepIndex[questId] = idx;
            State.Save.questStepProgress[questId] = 0;
            _distinct.Remove(questId);
            _festival.Remove(questId);
            State.MarkDirty();
            OnProgressChanged?.Invoke();

            if (idx >= def.steps.Count) { CompleteQuest(questId); return; }
            Debug.Log($"[Quest] {questId} → step {idx} ({def.steps[idx].type})");
            TryPresentCutscene(questId);
            TryGrantDeliverItem(questId);
        }

        void CompleteQuest(string questId)
        {
            var def = GameDatabase.Quest(questId);
            State.Save.activeQuests.Remove(questId);
            State.Save.questStepIndex.Remove(questId);
            State.Save.questStepProgress.Remove(questId);
            if (!State.Save.questsCompleted.Contains(questId))
                State.Save.questsCompleted.Add(questId);

            if (def.rewards != null)
            {
                if (def.rewards.coins > 0) State.AddCoins(def.rewards.coins);
                if (def.rewards.xp > 0) State.AddXp(def.rewards.xp);
                foreach (var item in def.rewards.items ?? Enumerable.Empty<string>())
                    GrantItem(item);
            }

            Debug.Log($"[Quest] COMPLETED {questId}: {def.title} " +
                      $"(+{def.rewards?.coins ?? 0}c, +{def.rewards?.xp ?? 0}xp)");

            // giver reacts, then the chain continues
            if (def.giver != null && GameDatabase.Npc(def.giver)?.dialogue != null &&
                GameDatabase.Npc(def.giver).dialogue.TryGetValue("quest_complete", out var line))
                UI.DialogueUI.Show(GameDatabase.Npc(def.giver).name, line);

            State.Save.activeQuest = def.unlocks;
            State.MarkDirty();
            OnQuestCompleted?.Invoke(def);
            // quests_completed (q_eternal_bloom) is a threshold poll against the
            // completed-count total, not an incremental counter — see PollThresholdSteps.
            // Save.questsCompleted already includes questId above, so the poll sees it.

            if (def.unlocks != null) StartQuest(def.unlocks);
        }

        /// <summary>deposit_seeds: consume `count` seeds spread across at least `kinds`
        /// distinct owned crop types on the first active matching step. Called by
        /// LandmarkController when a restored "lore_journal_2" landmark is tapped.</summary>
        public bool TryDepositSeeds()
        {
            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null || step.type != "deposit_seeds") continue;

                var owned = State.Save.seeds.Where(kv => kv.Value > 0).ToList();
                int total = owned.Sum(kv => kv.Value);
                int neededCount = Mathf.Max(1, step.count);
                int neededKinds = Mathf.Max(1, step.kinds);
                if (owned.Count < neededKinds || total < neededCount) return false;

                int remaining = neededCount;
                foreach (var kv in owned.OrderByDescending(kv => kv.Value))
                {
                    if (remaining <= 0) break;
                    int take = Mathf.Min(kv.Value, remaining);
                    State.Save.seeds[kv.Key] -= take;
                    if (State.Save.seeds[kv.Key] <= 0) State.Save.seeds.Remove(kv.Key);
                    remaining -= take;
                }
                State.MarkDirty();
                Debug.Log($"[Quest] {questId} deposited {neededCount} seeds across {owned.Count} kinds");
                AdvanceStep(questId);
                return true;
            }
            return false;
        }

        /// <summary>craft simplification: a raw material collection (ItemGranted) directly
        /// advances any active craft step whose output item maps from it (CraftInputs).</summary>
        void HandleCraftInput(string producedItem)
        {
            foreach (var output in CraftInputs.Where(kv => kv.Value == producedItem).Select(kv => kv.Key).ToList())
                Bump("craft", s => s.item == output);
        }

        /// <summary>Reward items: tools/animals/seeds route to their inventories; story
        /// items and trophies land in cosmeticsOwned (existing snapshot field).</summary>
        void GrantItem(string item)
        {
            if (GameDatabase.Tools.Any(t => t.id == item))
            {
                if (!State.Save.tools.Contains(item)) State.Save.tools.Add(item);
            }
            else if (item.StartsWith("pet_") || GameDatabase.Animals.Any(a => a.id == item))
            {
                string animalId = item.StartsWith("pet_") ? item.Substring(4) : item;
                if (!State.Save.animals.Contains(animalId)) State.Save.animals.Add(animalId);
            }
            else if (GameDatabase.Crop(item) != null)
            {
                State.Save.seeds.TryGetValue(item, out var n);
                State.Save.seeds[item] = n + 1;
            }
            else if (!State.Save.cosmeticsOwned.Contains(item))
            {
                State.Save.cosmeticsOwned.Add(item);
            }
            GameEvents.ItemGranted(item);
        }

        // ── step evaluation ───────────────────────────────────────────────────

        void Bump(string type, Func<QuestStep, bool> match, bool complete = false)
        {
            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null || step.type != type || !match(step)) continue;

                if (complete) { AdvanceStep(questId); continue; }

                int progress = StepProgress(questId) + 1;
                State.Save.questStepProgress[questId] = progress;
                State.MarkDirty();
                OnProgressChanged?.Invoke();
                if (progress >= Mathf.Max(1, step.count)) AdvanceStep(questId);
            }
        }

        void OnHarvest(string cropId)
        {
            Bump("harvest_crop", s => s.crop == null || s.crop == cropId);
            Bump("harvest_any", s => true);
            // FarmingSystem sells on harvest, so sale-steps count harvests (no market UI in scope)
            Bump("sell_any", s => true);
            BumpDistinct("sell_distinct", cropId);
            if (TimeManager.Instance != null && TimeManager.Instance.IsNight)
                Bump("harvest_at_night", s => true);
        }

        void OnTalkedTo(string npcId)
        {
            BumpDistinct("talk_to", npcId, s => s.npcs != null && s.npcs.Contains(npcId));
            TryDeliver(npcId);
            HandleFestivalTalk(npcId);

            // side quests activate on first chat with their giver
            foreach (var quest in GameDatabase.Quests)
            {
                if (quest.giver != npcId || IsActive(quest.id) || IsCompleted(quest.id)) continue;
                bool inChain = GameDatabase.Quests.Any(other => other.unlocks == quest.id);
                if (!inChain && quest.id != "q_arrival") StartQuest(quest.id);
            }
        }

        /// <summary>deliver: talking to a step's target while holding its item completes
        /// the step and consumes the item (quest activation granted it, see
        /// TryGrantDeliverItem).</summary>
        void TryDeliver(string npcId)
        {
            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null || step.type != "deliver" || step.target != npcId) continue;
                if (!State.Save.heldItems.Contains(step.item)) continue;

                State.Save.heldItems.Remove(step.item);
                State.MarkDirty();
                Debug.Log($"[Quest] {questId} delivered {step.item} to {npcId}");
                AdvanceStep(questId);
            }
        }

        /// <summary>host_festival simplification (user-approved, mirrors q_festival's
        /// own data pattern): FestivalTalkQuota distinct NPCs of one Sacred Garden
        /// (the four act-3 regions) within a single in-game day hosts that garden's
        /// festival; step.count distinct gardens complete the step.</summary>
        void HandleFestivalTalk(string npcId)
        {
            string gardenId = GameDatabase.Npc(npcId)?.region;
            var garden = gardenId != null ? GameDatabase.Region(gardenId) : null;
            if (garden == null || garden.act != 3) return;

            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null || step.type != "host_festival") continue;

                if (!_festival.TryGetValue(questId, out var tally))
                    _festival[questId] = tally = new FestivalTally { day = Today() };
                if (tally.day != Today())
                {
                    tally.talked.Clear();
                    tally.day = Today();
                }

                if (!tally.talked.TryGetValue(gardenId, out var talkedToday))
                    tally.talked[gardenId] = talkedToday = new HashSet<string>();
                if (!talkedToday.Add(npcId) || talkedToday.Count < FestivalTalkQuota) continue;

                if (!_distinct.TryGetValue(questId, out var hosted))
                    _distinct[questId] = hosted = new HashSet<string>();
                if (!hosted.Add(gardenId)) continue;

                State.Save.questStepProgress[questId] = hosted.Count;
                State.MarkDirty();
                OnProgressChanged?.Invoke();
                Debug.Log($"[Quest] {questId} hosted a festival in {gardenId} " +
                          $"({hosted.Count}/{Mathf.Max(1, step.count)}, day {tally.day})");
                if (hosted.Count >= Mathf.Max(1, step.count)) AdvanceStep(questId);
            }
        }

        void BumpDistinct(string type, string key, Func<QuestStep, bool> extraMatch = null)
        {
            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null || step.type != type) continue;
                if (extraMatch != null && !extraMatch(step)) continue;

                if (!_distinct.TryGetValue(questId, out var set))
                    _distinct[questId] = set = new HashSet<string>();
                if (!set.Add(key)) continue;

                State.Save.questStepProgress[questId] = set.Count;
                State.MarkDirty();
                OnProgressChanged?.Invoke();

                int required = type == "talk_to"
                    ? (step.npcs?.Count ?? 1)
                    : Mathf.Max(1, step.count);
                if (set.Count >= required) AdvanceStep(questId);
            }
        }

        /// <summary>Threshold/state steps evaluated by polling: soil_above,
        /// restoration_above, own_animals, own_tools, move_to, explore,
        /// quests_completed.</summary>
        void PollThresholdSteps()
        {
            if (State == null) return;
            foreach (var questId in ActiveQuests().ToList())
            {
                var step = CurrentStep(questId);
                if (step == null) continue;
                switch (step.type)
                {
                    case "soil_above":
                        int healthy = State.CurrentFarm.plots.Values.Count(p => p.soilHealth >= step.threshold);
                        if (healthy >= Mathf.Max(1, step.count)) AdvanceStep(questId);
                        break;

                    case "restoration_above":
                        string region = step.region ?? State.Save.currentRegion;
                        if (State.FarmFor(region).restoration >= step.threshold) AdvanceStep(questId);
                        break;

                    case "own_animals":
                        if (State.Save.animals.Count >= Mathf.Max(1, step.count)) AdvanceStep(questId);
                        break;

                    case "own_tools":
                        if (State.Save.tools.Count >= Mathf.Max(1, step.count)) AdvanceStep(questId);
                        break;

                    // q_eternal_bloom fix (user-approved 2026-07-05): a threshold poll
                    // against the total completed-quest count, not an incremental
                    // per-completion counter — the old Bump() approach needed 59 MORE
                    // completions after activation, which is impossible with 60 total.
                    case "quests_completed":
                        if (State.Save.questsCompleted.Count >= Mathf.Max(1, step.count)) AdvanceStep(questId);
                        break;

                    // explore: proximity to a code-side target position (data has no
                    // coordinates for crystal_cave / crystal_cave_depths / crash_site —
                    // see World.ExploreTargets). Same polling shape as move_to below.
                    case "explore":
                        if (World.ExploreTargets.TryGetPosition(step.target, out var targetPos))
                        {
                            var explorer = FindFirstObjectByType<World.PlayerController>();
                            if (explorer != null && Vector2.Distance(explorer.transform.position, targetPos) < 3f)
                                AdvanceStep(questId);
                        }
                        break;

                    case "move_to":
                        // sole data use: "starting_plot" — the lush core of the start region
                        var player = FindFirstObjectByType<World.PlayerController>();
                        if (player != null && World.RegionManager.Instance != null &&
                            World.RegionManager.Instance.TryGetBounds("starlight_vale", out var b) &&
                            Vector2.Distance(player.transform.position, b.center) < 4f)
                            AdvanceStep(questId);
                        break;
                }
            }
        }
    }
}
