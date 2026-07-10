// Cosmic Bloom — small pulsing indicator above interactable world objects
// (landmarks, NPCs) so they read as "tap me" instead of inert scenery. Purely
// visual: no gameplay behavior, doesn't gate whether Interact() can be called.
using UnityEngine;

namespace CosmicBloom.World
{
    public class InteractionCue : MonoBehaviour
    {
        const float PulseSpeed = 2.2f;
        const float MinAlpha = 0.35f;
        const float MaxAlpha = 0.9f;
        const float MinScale = 0.85f;
        const float MaxScale = 1.15f;

        SpriteRenderer _sr;
        Vector3 _baseScale;
        float _phase;
        Color _baseColor;

        /// <summary>Adds a pulsing dot above `target`, tinted `color`. Returns the cue so
        /// callers can toggle it (e.g. dim/hide once a landmark no longer needs advertising).</summary>
        public static InteractionCue Attach(GameObject target, Color color, float yOffset = 0.9f, float scale = 0.16f)
        {
            var go = new GameObject("InteractionCue");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square(16, 2);
            sr.color = color;
            sr.sortingOrder = 30;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;

            var cue = go.AddComponent<InteractionCue>();
            cue._sr = sr;
            cue._baseScale = go.transform.localScale;
            cue._baseColor = color;
            cue._phase = Random.Range(0f, Mathf.PI * 2f);   // desync multiple cues
            return cue;
        }

        /// <summary>Show/hide without destroying (e.g. once a landmark's restore-need goes away).</summary>
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        /// <summary>Retint (e.g. gold "needs restoring" -> cyan "still tap me" once repeatable).</summary>
        public void SetColor(Color color) => _baseColor = color;

        void Update()
        {
            _phase += Time.deltaTime * PulseSpeed;
            float wave = (Mathf.Sin(_phase) + 1f) * 0.5f;   // 0..1
            transform.localScale = _baseScale * Mathf.Lerp(MinScale, MaxScale, wave);
            if (_sr != null)
            {
                var c = _baseColor;
                c.a = Mathf.Lerp(MinAlpha, MaxAlpha, wave);
                _sr.color = c;
            }
        }
    }
}
