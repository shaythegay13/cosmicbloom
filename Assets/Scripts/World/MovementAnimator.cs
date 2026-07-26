// Cosmic Bloom — procedural movement-animation stopgap. Every character
// (player, all 50 NPCs) currently renders one static Gemini pose (flagged as
// a known gap when the player art stopgap was built, P3-Phase 9 cont.,
// 2026-07-10) — with no walk-cycle frames, movement read as "sliding" rather
// than "not animated". Real fix is new Gemini art batches (directional/walk
// frames), blocked on AI Studio credits regardless of any code change here.
// This is a code-only substitute: a hop-bob + squash-stretch driven purely by
// the character's own frame-to-frame world-position delta, so it works
// unmodified for both PlayerController's tap-to-move and NpcController's
// schedule walking without either controller knowing this exists.
using UnityEngine;

namespace CosmicBloom.World
{
    public class MovementAnimator : MonoBehaviour
    {
        /// <summary>Child transform carrying the sprite. Animating a child (not this
        /// object's own transform) keeps colliders and movement-target math on
        /// the root untouched by the bob/squash offsets.</summary>
        public Transform visual { get; private set; }

        [Tooltip("World units/sec below which the character is treated as stopped.")]
        public float moveThreshold = 0.05f;

        [Tooltip("Bob/squash cycles per world-unit traveled, so a faster mover (tool " +
                 "tiers, NPC walkSpeed) bobs faster too, matching stride rather than a fixed clock.")]
        public float cyclesPerUnit = 1.4f;

        public float bobHeight = 0.06f;
        public float squashAmount = 0.07f;
        public float settleSpeed = 12f;

        Vector3 _lastPos;
        Vector3 _basePos;
        Vector3 _baseScale;
        float _phase;
        bool _initialized;

        /// <summary>Must be called once by the spawner right after AddComponent, passing the
        /// child that carries the sprite. AddComponent runs Awake/OnEnable synchronously
        /// before the caller can assign anything, so rest-pose capture can't happen there —
        /// it has to happen here, once the real visual transform is known.</summary>
        public void Init(Transform visualTransform)
        {
            visual = visualTransform;
            _basePos = visual.localPosition;
            _baseScale = visual.localScale;
            _initialized = true;
        }

        void OnEnable()
        {
            _lastPos = transform.position;
        }

        void LateUpdate()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            float dist = Vector3.Distance(pos, _lastPos);
            _lastPos = pos;
            bool moving = dt > 0f && dist / dt > moveThreshold;

            if (moving)
            {
                _phase += dist * cyclesPerUnit * Mathf.PI * 2f;
                float bobT = Mathf.Abs(Mathf.Sin(_phase));               // 0 at ground contact, 1 at hop peak
                float squash = Mathf.Lerp(-squashAmount, squashAmount, bobT);   // squashed at contact, stretched at peak

                visual.localPosition = _basePos + new Vector3(0f, bobHeight * bobT, 0f);
                visual.localScale = new Vector3(
                    _baseScale.x * (1f - squash),
                    _baseScale.y * (1f + squash),
                    _baseScale.z);
            }
            else
            {
                float t = settleSpeed * dt;
                visual.localPosition = Vector3.Lerp(visual.localPosition, _basePos, t);
                visual.localScale = Vector3.Lerp(visual.localScale, _baseScale, t);
            }
        }
    }
}
