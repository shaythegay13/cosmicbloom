// Cosmic Bloom — small transient particle bursts for till/water/harvest, so a
// tool action reads as landing instead of just silently changing a tile's
// color. Purely visual/decorative: no gameplay state, nothing persisted.
// Reuses ProceduralSprites.Square as a tintable "dot" — consistent with the
// game's blocky pixel-art look, no new art assets needed for a sub-second burst.
using UnityEngine;

namespace CosmicBloom.Farming
{
    public static class FarmActionEffects
    {
        struct Spec
        {
            public Color32 color;
            public int count;
            public float speed;
            public float gravity;   // negative = drifts upward
            public float duration;
            public float dotSize;
        }

        static readonly Spec TillSpec = new Spec
        {
            color = new Color32(0x8B, 0x69, 0x14, 0xFF), count = 6, speed = 1.4f,
            gravity = 0.6f, duration = 0.35f, dotSize = 0.10f,
        };
        static readonly Spec WaterSpec = new Spec
        {
            color = new Color32(0x44, 0xAA, 0xFF, 0xFF), count = 7, speed = 2.0f,
            gravity = 3.5f, duration = 0.45f, dotSize = 0.09f,
        };
        static readonly Spec HarvestSpec = new Spec
        {
            color = new Color32(0xFF, 0xE0, 0x66, 0xFF), count = 8, speed = 1.6f,
            gravity = -1.2f, duration = 0.6f, dotSize = 0.11f,
        };
        // P3-Phase 17 live-verify finding C: planting was the only farm action with
        // zero visual confirmation — a leafy-green upward burst, distinct from till's
        // dirt-brown and water's blue.
        static readonly Spec PlantSpec = new Spec
        {
            color = new Color32(0x6B, 0xC7, 0x4A, 0xFF), count = 6, speed = 1.2f,
            gravity = -0.8f, duration = 0.5f, dotSize = 0.09f,
        };

        /// <summary>Fire-and-forget burst at a plot's world position for a farm action.</summary>
        public static void Spawn(string action, Vector3 position)
        {
            Spec spec = action switch
            {
                "till" => TillSpec,
                "water" => WaterSpec,
                "harvest" => HarvestSpec,
                "plant" => PlantSpec,
                _ => default,
            };
            if (spec.count <= 0) return;

            var sprite = World.ProceduralSprites.Square(16, 1);
            for (int i = 0; i < spec.count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / spec.count + Random.Range(-0.3f, 0.3f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.3f);

                var go = new GameObject($"FX_{action}");
                go.transform.position = position;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = spec.color;
                sr.sortingOrder = 20;
                var mat = World.ProceduralSprites.LitMaterial;
                if (mat != null) sr.sharedMaterial = mat;
                go.transform.localScale = Vector3.one * spec.dotSize;

                var particle = go.AddComponent<FarmActionEffectParticle>();
                particle.Init(dir * spec.speed, spec.gravity, spec.duration);
            }
        }
    }

    /// <summary>One self-destroying particle: drifts along an initial velocity (with
    /// gravity), scales down, and fades out over its lifetime.</summary>
    public class FarmActionEffectParticle : MonoBehaviour
    {
        Vector2 _velocity;
        float _gravity;
        float _duration;
        float _age;
        SpriteRenderer _sr;
        Vector3 _startScale;

        public void Init(Vector2 velocity, float gravity, float duration)
        {
            _velocity = velocity;
            _gravity = gravity;
            _duration = duration;
            _sr = GetComponent<SpriteRenderer>();
            _startScale = transform.localScale;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _duration) { Destroy(gameObject); return; }

            _velocity.y -= _gravity * Time.deltaTime;
            transform.position += (Vector3)(_velocity * Time.deltaTime);

            float t = _age / _duration;
            transform.localScale = _startScale * (1f - 0.5f * t);
            if (_sr != null)
            {
                var c = _sr.color;
                c.a = 1f - t;
                _sr.color = c;
            }
        }
    }
}
