// Cosmic Bloom — P3-Phase 17 live-verify finding D: the ground-tap contextual
// action (till/plant/water/harvest) had no on-screen indication of what tapping
// would actually do. This is a small always-on label that follows the player and
// shows the verb for whatever tile is currently under them, so the action isn't a
// silent guess. World-space TextMesh (same simple, camera-independent approach as
// FarmActionEffects' particles) rather than a Canvas element — it just tracks the
// player's transform directly, no screen/world conversion or render-mode plumbing.
using CosmicBloom.World;
using UnityEngine;

namespace CosmicBloom.Farming
{
    public class FarmActionPrompt : MonoBehaviour
    {
        const float YOffset = 0.85f;

        Transform _player;
        TextMesh _mesh;

        public static FarmActionPrompt Create(Transform player)
        {
            var go = new GameObject("FarmActionPrompt");
            var mesh = go.AddComponent<TextMesh>();
            mesh.characterSize = 0.11f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(1f, 1f, 1f, 0.95f);
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mesh.font.material;
            mr.sortingOrder = 31;   // above FarmActionEffects particles (20) and InteractionCue (30)

            var prompt = go.AddComponent<FarmActionPrompt>();
            prompt._player = player;
            prompt._mesh = mesh;
            return prompt;
        }

        void LateUpdate()
        {
            if (_player == null) { Destroy(gameObject); return; }
            transform.position = _player.position + Vector3.up * YOffset;

            string label = null;
            var regionId = RegionManager.Instance != null ? RegionManager.Instance.CurrentRegionId : null;
            var grid = regionId != null ? FarmGrid.ForRegion(regionId) : null;
            if (grid != null) label = LabelFor(grid.ContextActionAt(_player.position));

            _mesh.text = label ?? string.Empty;
        }

        static string LabelFor(string action) => action switch
        {
            "till" => "Tap to till",
            "plant" => "Tap to plant",
            "water" => "Tap to water",
            "harvest" => "Tap to harvest",
            _ => null,
        };

        /// <summary>Test/inspection access — null when nothing is currently shown.</summary>
        public string CurrentLabel => _mesh != null && !string.IsNullOrEmpty(_mesh.text) ? _mesh.text : null;
    }
}
