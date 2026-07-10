// Cosmic Bloom — one behavior for all 15 animals (livestock/pet/helper),
// entirely data-driven from animals.json: palette tint, produce type and
// timer. Wanders near its home point; tap to pet, or collect ready produce.
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
using System.Collections.Generic;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public class AnimalController : MonoBehaviour, IInteractable
    {
        public AnimalDef Def { get; private set; }

        GameState _state;
        Vector3 _home;
        Vector3 _wanderTarget;
        float _nextWander;
        SpriteRenderer _body;
        bool _usingGeneratedSprite;

        // one produce clock per species, shared by every spawned instance
        static readonly Dictionary<string, long> _produceReadyBySpecies = new();

        /// <summary>Session/test hygiene — cleared alongside AnimalSpawner.ClearAll()
        /// and on SpawnOwned() so a fresh session restarts all timers.</summary>
        public static void ResetProduceClocks() => _produceReadyBySpecies.Clear();

        public bool ProduceReady =>
            Def.produceMinutes > 0 &&
            _produceReadyBySpecies.TryGetValue(Def.id, out var readyAt) && NowMs() >= readyAt;

        public Vector3 InteractPosition => transform.position;

        static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static AnimalController Spawn(AnimalDef def, Vector3 home, GameState state)
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
            // only the first instance of a species starts the clock — later
            // instances (other regions) must not push the ready time back
            if (def.produceMinutes > 0 && !_produceReadyBySpecies.ContainsKey(def.id))
                animal.ScheduleProduce();
            return animal;
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
            // gentle wander around home
            if (Time.time >= _nextWander)
            {
                _nextWander = Time.time + Random.Range(2.5f, 6f);
                _wanderTarget = _home + (Vector3)(Random.insideUnitCircle * 2.2f);
            }
            transform.position = Vector3.MoveTowards(transform.position, _wanderTarget, 0.9f * Time.deltaTime);

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

        public void Interact(PlayerController player)
        {
            if (ProduceReady && _state != null)
            {
                int coins = Mathf.Max(2, Def.cost / 40);
                _state.AddCoins(coins);
                ScheduleProduce();
                GameEvents.ItemGranted(Def.produce);
                Debug.Log($"[Animal] collected {Def.produce} from {Def.id} (+{coins}c)");
                UI.DialogueUI.Show(Def.name, $"You collect {Def.produce.Replace('_', ' ')}. (+{coins} coins)");
            }
            else
            {
                Debug.Log($"[Animal] petted {Def.id}");
                UI.DialogueUI.Show(Def.name, $"{Def.name} nuzzles you happily.");
            }
        }
    }
}
