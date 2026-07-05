// Cosmic Bloom — one behavior for all 15 animals (livestock/pet/helper),
// entirely data-driven from animals.json: palette tint, produce type and
// timer. Wanders near its home point; tap to pet, or collect ready produce.
//
// DECISION (overnight run): produce has no price data, so collecting grants
// coins = max(2, cost/40) alongside the GameEvents.ItemGranted signal that
// `collect` quest steps (e.g. q_egg_basket's glowing_egg ×5) consume.
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
        long _produceReadyAt;
        SpriteRenderer _body;

        public bool ProduceReady =>
            Def.produceMinutes > 0 && NowMs() >= _produceReadyAt;

        public Vector3 InteractPosition => transform.position;

        static long NowMs() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static AnimalController Spawn(AnimalDef def, Vector3 home, GameState state)
        {
            var go = new GameObject($"Animal_{def.id}");
            go.transform.position = home;

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = ProceduralSprites.Critter();
            body.color = ProceduralSprites.ParseHex(
                def.palette != null && def.palette.TryGetValue("body", out var b) ? b : null);
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
            animal.ScheduleProduce();
            return animal;
        }

        void ScheduleProduce()
        {
            if (Def.produceMinutes > 0)
                _produceReadyAt = NowMs() + (long)(Def.produceMinutes * 60_000f);
        }

        /// <summary>Test/debug hook: make produce collectable now.</summary>
        public void ForceProduceReady() => _produceReadyAt = NowMs() - 1;

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
                var c = ProceduralSprites.ParseHex(
                    Def.palette != null && Def.palette.TryGetValue("body", out var b) ? b : null);
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
