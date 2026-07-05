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
// Step support (DECISION, overnight run): step types whose backing systems
// are in scope run on real events/polling. Types requiring out-of-scope
// systems (fishing, crafting, festivals, exploration caves, cosmic events,
// trading) surface in the quest log with an explicit "[skip — system
// pending]" affordance rather than silently auto-completing, so morning
// review can see exactly what was stubbed. List: catch_fish, craft, explore,
// wait_for, witness_event, host_festival, challenge_run, trade_visitors,
// deposit_seeds, deliver, and collect of non-animal items.
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
        static readonly HashSet<string> PendingSystemSteps = new()
        {
            "catch_fish", "craft", "explore", "wait_for", "witness_event",
            "host_festival", "challenge_run", "trade_visitors", "deposit_seeds", "deliver",
        };

        // in-memory distinct-tracking for talk_to / sell_distinct style steps
        readonly Dictionary<string, HashSet<string>> _distinct = new();

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
            GameEvents.OnItemGranted += item => Bump("collect", s => s.item == item);

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
            s != null && (PendingSystemSteps.Contains(s.type) ||
                          (s.type == "collect" && !IsAnimalProduce(s.item)));

        static bool IsAnimalProduce(string item) =>
            item != null && GameDatabase.Animals.Any(a => a.produce == item);

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
        }

        void TryPresentCutscene(string questId)
        {
            var step = CurrentStep(questId);
            var def = GameDatabase.Quest(questId);
            if (step == null || step.type != "cutscene") return;
            string speaker = def.giver != null ? (GameDatabase.Npc(def.giver)?.name ?? "???") : "Mysterious Guide";
            UI.DialogueUI.Show(speaker, def.summary, () => AdvanceStep(questId));
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
            State.MarkDirty();
            OnProgressChanged?.Invoke();

            if (idx >= def.steps.Count) { CompleteQuest(questId); return; }
            Debug.Log($"[Quest] {questId} → step {idx} ({def.steps[idx].type})");
            TryPresentCutscene(questId);
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
            Bump("quests_completed", s => true);

            if (def.unlocks != null) StartQuest(def.unlocks);
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

            // side quests activate on first chat with their giver
            foreach (var quest in GameDatabase.Quests)
            {
                if (quest.giver != npcId || IsActive(quest.id) || IsCompleted(quest.id)) continue;
                bool inChain = GameDatabase.Quests.Any(other => other.unlocks == quest.id);
                if (!inChain && quest.id != "q_arrival") StartQuest(quest.id);
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
        /// restoration_above, own_animals, own_tools, move_to.</summary>
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
