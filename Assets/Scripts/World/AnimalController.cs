// Cosmic Bloom — one behavior for all 15 animals, entirely data-driven from
// animals.json: palette tint, produce type and timer.
//
// P3-Phase 18 splits BEHAVIOR by AnimalRole (not by animals.json's descriptive
// `kind`):
//   LIVESTOCK  — sits at its home point (the player-chosen placement cell, or
//                the default paddock spot) and wanders gently around it. Tap to
//                collect ready produce.
//   COMPANION  — walks Phase 16's NPC daily schedule between NpcSchedule stops
//                as the day period changes, and talks when tapped.
// Produce is untouched by the split: a companion that has produce in data
// collects exactly like livestock (see the DECISION in AnimalRoles).
//
// DECISION (overnight run): produce has no price data, so collecting grants
// coins = max(2, cost/40) alongside the GameEvents.ItemGranted signal that
// `collect` quest steps (e.g. q_egg_basket's glowing_egg ×5) consume.
//
// DECISION (Phase 8 economy fix): the produce clock is per SPECIES, not per
// instance. A shared animal spawns an instance in every owned region (up to 5),
// but owning it is one purchase — so it gets exactly one produce stream:
// collecting from any instance consumes the produce and reschedules the species
// clock, and every other instance stops reading ready. This closes the Phase 4b
// flagged 5x-income implication while keeping the multi-region instances as
// pure world ambience.
//
// DATA GAP (P3-Phase 18, flagged exactly as Phase 16 flagged schedule-aware NPC
// dialogue): animals.json has NO dialogue field of any kind — no intro/idle
// keys like npcs.json's `dialogue` object, nothing keyed by time of day or
// location. The only authored per-animal prose is `notes`, so a companion's
// dialogue line IS its notes text, shown as an observation. No lines were
// invented to fill the gap. If animals.json ever grows a `dialogue` object,
// wire it in CompanionLine() below — the intro-vs-idle split is already
// tracked here, it just has nothing distinct to say yet.
using System.Collections.Generic;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public class AnimalController : MonoBehaviour, IInteractable
    {
        public AnimalDef Def { get; private set; }

        /// <summary>Which region this instance was spawned into (P3-Phase 18).</summary>
        public string RegionId { get; private set; }

        public AnimalRole Role => AnimalRoles.RoleOf(Def.id);
        public bool IsCompanion => Role == AnimalRole.Companion;

        [Tooltip("World units/sec between companion schedule stops (P3-Phase 18)")]
        public float walkSpeed = 1.3f;   // a touch slower than NPCs' 1.6 — pets amble

        /// <summary>How far an animal drifts from its home point while idling.
        /// Livestock keep the roomy original paddock amble. A COMPANION uses a much
        /// tighter leash: its stops sit ~4.4 units apart, so a 2.2-unit wander could
        /// put it in nearly the same spot at two different times of day and make the
        /// schedule unreadable — the whole point of giving it one.</summary>
        public float WanderRadius => IsCompanion ? 0.6f : 2.2f;

        GameState _state;
        Vector3 _home;
        Vector3 _wanderTarget;
        float _nextWander;
        SpriteRenderer _body;
        bool _usingGeneratedSprite;

        // companion schedule state (mirrors NpcController's)
        Vector3 _regionCenter;
        float _ringAngle;
        Vector3 _scheduleTarget;
        bool _hasScheduleTarget;
        bool _subscribed;

        // one produce clock per species, shared by every spawned instance
        static readonly Dictionary<string, long> _produceReadyBySpecies = new();
        // companions have no npcsMet equivalent in the save; track first meeting
        // in-session so the intro/idle split is ready for data that doesn't exist yet
        static readonly HashSet<string> _metCompanions = new();

        /// <summary>Session/test hygiene — cleared alongside AnimalSpawner.ClearAll()
        /// and on SpawnOwned() so a fresh session restarts all timers.</summary>
        public static void ResetProduceClocks()
        {
            _produceReadyBySpecies.Clear();
            _metCompanions.Clear();
        }

        public bool ProduceReady =>
            Def.produceMinutes > 0 &&
            _produceReadyBySpecies.TryGetValue(Def.id, out var readyAt) && NowMs() >= readyAt;

        public Vector3 InteractPosition => transform.position;

        static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static AnimalController Spawn(AnimalDef def, Vector3 home, GameState state,
                                             string regionId = null, Vector3 regionCenter = default,
                                             float ringAngle = 0f)
        {
            var go = new GameObject($"Animal_{def.id}");
            go.transform.position = home;

            // Real generated art if it loaded (Tools/art/build-cosmic.mjs output) — a
            // full critter sprite already carrying this animal's palette, so no tint.
            // Falls back to the tinted procedural blob when nothing was generated.
            var generated = GeneratedSprites.Animal(def.id);
            bool usingGenerated = generated != null;

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = usingGenerated ? generated : ProceduralSprites.Critter();
            body.color = usingGenerated
                ? Color.white
                : ProceduralSprites.ParseHex(def.palette != null && def.palette.TryGetValue("body", out var b) ? b : null);
            body.sortingOrder = 8;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) body.sharedMaterial = mat;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.5f;

            var animal = go.AddComponent<AnimalController>();
            animal.Def = def;
            animal._state = state;
            animal._home = home;
            animal._body = body;
            animal._usingGeneratedSprite = usingGenerated;
            animal.RegionId = regionId;
            animal._regionCenter = regionCenter;
            animal._ringAngle = ringAngle;
            // only the first instance of a species starts the clock — later
            // instances (other regions) must not push the ready time back
            if (def.produceMinutes > 0 && !_produceReadyBySpecies.ContainsKey(def.id))
                animal.ScheduleProduce();

            // P3-Phase 18: a companion starts the day already at its current stop
            // (snap on spawn, walk on later transitions) — NpcController's pattern.
            if (animal.IsCompanion && TimeManager.Instance != null)
            {
                go.transform.position = CompanionSchedule.StopPosition(
                    CompanionSchedule.StopFor(TimeManager.Instance.CurrentPeriod), regionCenter, ringAngle);
                animal._home = go.transform.position;
            }
            animal.Subscribe();   // Def is set now; the OnEnable call above bailed out

            // P3-Phase 21: visible "produce waiting" sparkle. Attached to every animal;
            // it stays hidden for species with no produce data and shows only when the
            // shared per-species clock is actually ready.
            ProduceReadyIndicator.Attach(go);
            return animal;
        }

        /// <summary>Relocate a placed livestock animal (P3-Phase 18 placement).</summary>
        public void MoveHome(Vector3 world)
        {
            _home = world;
            transform.position = world;
            _wanderTarget = world;
            _nextWander = 0f;
        }

        void OnEnable() => Subscribe();
        void Start() => Subscribe();

        void Subscribe()
        {
            // OnEnable fires during AddComponent<AnimalController>(), BEFORE Spawn()
            // assigns Def — so Def is genuinely null on the first call and IsCompanion
            // would throw. Start() calls Subscribe again once Def is set.
            if (_subscribed || Def == null || !IsCompanion || TimeManager.Instance == null) return;
            TimeManager.Instance.OnPeriodChanged += OnPeriodChanged;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed && TimeManager.Instance != null)
                TimeManager.Instance.OnPeriodChanged -= OnPeriodChanged;
        }

        /// <summary>New day period → walk to this companion's stop for it.</summary>
        void OnPeriodChanged(TimeManager.DayPeriod period)
        {
            _scheduleTarget = CompanionSchedule.StopPosition(
                CompanionSchedule.StopFor(period), _regionCenter, _ringAngle);
            _hasScheduleTarget = true;
        }

        void ScheduleProduce()
        {
            if (Def.produceMinutes > 0)
                _produceReadyBySpecies[Def.id] = NowMs() + (long)(Def.produceMinutes * 60_000f);
        }

        /// <summary>Test/debug hook: make produce collectable now (species-wide).</summary>
        public void ForceProduceReady() => _produceReadyBySpecies[Def.id] = NowMs() - 1;

        void Update()
        {
            if (_hasScheduleTarget)
            {
                // companion walking to a schedule stop — no wander until it arrives
                transform.position = Vector3.MoveTowards(
                    transform.position, _scheduleTarget, walkSpeed * Time.deltaTime);
                if ((transform.position - _scheduleTarget).sqrMagnitude < 0.0001f)
                {
                    _hasScheduleTarget = false;
                    _home = _scheduleTarget;   // wander around wherever the schedule left it
                }
            }
            else
            {
                // gentle wander around home
                if (Time.time >= _nextWander)
                {
                    _nextWander = Time.time + Random.Range(2.5f, 6f);
                    _wanderTarget = _home + (Vector3)(Random.insideUnitCircle * WanderRadius);
                }
                transform.position = Vector3.MoveTowards(transform.position, _wanderTarget, 0.9f * Time.deltaTime);
            }

            // ready-produce glow cue
            if (_body != null && Def.produceMinutes > 0)
            {
                float glow = ProduceReady ? 1.25f : 1f;
                var c = _usingGeneratedSprite
                    ? Color.white
                    : ProceduralSprites.ParseHex(Def.palette != null && Def.palette.TryGetValue("body", out var b) ? b : null);
                _body.color = c * glow;
            }
        }

        /// <summary>The line a companion shows when tapped. See the DATA GAP note in the
        /// file header — animals.json carries no dialogue object, so this is the animal's
        /// authored `notes` prose shown as an observation, never invented content.</summary>
        public string CompanionLine(bool met)
        {
            if (!string.IsNullOrWhiteSpace(Def.notes)) return Def.notes;
            return met
                ? $"{Def.name} settles in beside you."
                : $"{Def.name} pads over and looks you up and down.";
        }

        public void Interact(PlayerController player)
        {
            // produce first, for either role — the role split is behavioral, not economic
            if (ProduceReady && _state != null)
            {
                int coins = Mathf.Max(2, Def.cost / 40);
                _state.AddCoins(coins);
                ScheduleProduce();
                GameEvents.ItemGranted(Def.produce);
                Debug.Log($"[Animal] collected {Def.produce} from {Def.id} (+{coins}c)");
                UI.DialogueUI.Show(Def.name, $"You collect {Def.produce.Replace('_', ' ')}. (+{coins} coins)");
                return;
            }

            if (IsCompanion)
            {
                bool met = _metCompanions.Contains(Def.id);
                string line = CompanionLine(met);
                if (!met) _metCompanions.Add(Def.id);
                Debug.Log($"[Animal] companion {Def.id} spoke (met={met})");
                UI.DialogueUI.Show(Def.name, line);
                return;
            }

            Debug.Log($"[Animal] petted {Def.id}");
            UI.DialogueUI.Show(Def.name, $"{Def.name} nuzzles you happily.");
        }
    }
}
