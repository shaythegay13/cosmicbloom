// Cosmic Bloom — Phase 2: visual waypoint for an active "explore" quest step.
// Purely a landmark for the player to walk toward; completion is the
// proximity poll in QuestEngine.PollThresholdSteps (same pattern as move_to).
using UnityEngine;

namespace CosmicBloom.World
{
    public class ExploreMarkerController : MonoBehaviour
    {
        static readonly Color MarkerColor = new Color(0.7f, 0.4f, 0.95f, 1f);   // mystic beacon

        public static ExploreMarkerController Spawn(Vector3 position)
        {
            var go = new GameObject("ExploreMarker");
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square(32, 8);
            sr.color = MarkerColor;
            sr.sortingOrder = 5;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;

            return go.AddComponent<ExploreMarkerController>();
        }

        SpriteRenderer _sr;
        void Awake() => _sr = GetComponent<SpriteRenderer>();

        // gentle pulse so the beacon reads as "active" against static world sprites
        void Update()
        {
            if (_sr == null) return;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.5f);
            _sr.color = new Color(MarkerColor.r, MarkerColor.g, MarkerColor.b, pulse);
        }
    }
}
