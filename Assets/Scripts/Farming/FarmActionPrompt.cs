// Cosmic Bloom — P3-Phase 17 live-verify finding D: the ground-tap contextual
// action (till/plant/water/harvest) had no on-screen indication of what tapping
// would actually do. This is a small always-on label that follows the player and
// shows the verb for whatever tile is currently under them, so the action isn't a
// silent guess. World-space TextMesh (same simple, camera-independent approach as
// FarmActionEffects' particles) rather than a Canvas element — it just tracks the
// player's transform directly, no screen/world conversion or render-mode plumbing.
//
// P3-Phase 21 adds a SECOND, smaller mesh beneath it carrying the planted crop's
// live crops.json facts (see CropInfo). It is deliberately a separate TextMesh
// rather than a newline appended to the verb: `CurrentLabel` is the verb contract
// Phase 17's tests assert against, and the two lines want different type sizes
// anyway (a verb is a button prompt, the info line is fine print).
using CosmicBloom.World;
using UnityEngine;

namespace CosmicBloom.Farming
{
    public class FarmActionPrompt : MonoBehaviour
    {
        const float YOffset = 0.85f;
        // The info block sits ABOVE the verb and grows upward (lower-centre anchor).
        // Below the verb it covered the player's own sprite and anything standing
        // beside them; stacking upward keeps the characters clear at any line count.
        const float InfoYOffset = 1.30f;

        Transform _player;
        TextMesh _mesh;
        TextMesh _infoMesh;

        public static FarmActionPrompt Create(Transform player)
        {
            var go = new GameObject("FarmActionPrompt");
            var mesh = MakeMesh(go, 0.11f, new Color(1f, 1f, 1f, 0.95f));

            // child node so the info line can sit at its own height without fighting
            // the parent's per-frame follow of the player
            var infoGo = new GameObject("CropInfo");
            infoGo.transform.SetParent(go.transform, false);
            infoGo.transform.localPosition = new Vector3(0f, InfoYOffset - YOffset, 0f);
            // 0.055, not the verb's 0.11: the first live capture at 0.075 on ONE long
            // line ran off both edges of a 6.4-unit-wide camera. Short stacked lines at
            // roughly half the verb's size fit with room to spare.
            var infoMesh = MakeMesh(infoGo, 0.055f, new Color(0.85f, 0.94f, 1f, 0.95f));
            infoMesh.anchor = TextAnchor.LowerCenter;   // grows upward, away from the characters

            var prompt = go.AddComponent<FarmActionPrompt>();
            prompt._player = player;
            prompt._mesh = mesh;
            prompt._infoMesh = infoMesh;
            return prompt;
        }

        static TextMesh MakeMesh(GameObject go, float charSize, Color color)
        {
            var mesh = go.AddComponent<TextMesh>();
            mesh.characterSize = charSize;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mesh.font.material;
            mr.sortingOrder = 31;   // above FarmActionEffects particles (20) and InteractionCue (30)
            return mesh;
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
            // The facts come from CropInfo (crops.json + live PlotState) via the grid
            // that already owns this region's state — never assembled here.
            if (_infoMesh != null)
                _infoMesh.text = grid != null ? grid.CropInfoAt(_player.position).WorldLines : string.Empty;
        }

        static string LabelFor(string action) => action switch
        {
            "till" => "Tap to till",
            "plant" => "Tap to plant",
            "water" => "Tap to water",
            "harvest" => "Tap to harvest",
            _ => null,
        };

        /// <summary>Test/inspection access — null when nothing is currently shown.
        /// The VERB only; see <see cref="CurrentCropInfo"/> for the P3-Phase 21 line.</summary>
        public string CurrentLabel => _mesh != null && !string.IsNullOrEmpty(_mesh.text) ? _mesh.text : null;

        /// <summary>Test/inspection access — the crop-info line, null when no crop.</summary>
        public string CurrentCropInfo =>
            _infoMesh != null && !string.IsNullOrEmpty(_infoMesh.text) ? _infoMesh.text : null;
    }
}
