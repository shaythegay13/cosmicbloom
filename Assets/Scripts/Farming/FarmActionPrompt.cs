// Cosmic Bloom — P3-Phase 22: the player-following contextual prompt (the
// till/plant/water/harvest verb from P3-Phase 17, plus the P3-Phase 21 crop-info
// line beneath it).
//
// WHY THIS IS A CANVAS ELEMENT AND MUST STAY ONE
// ---------------------------------------------
// Phases 17 and 21 built both lines as world-space `TextMesh`, deliberately: it
// tracked the player's transform with no screen/world conversion. That choice
// silently coupled TEXT SIZE to CAMERA ZOOM, and P3-Phase 20's retune (ortho 7 ->
// 14) halved it without anything noticing — a TextMesh glyph is
// `fontSize * characterSize * 0.1` world units tall, so on a 1280px-tall portrait
// frame (45.7 px/unit at ortho 14):
//
//     verb  0.11 charSize * 48 fontSize -> 0.528 units -> 24.1 px, rasterised
//           from a 48px font atlas: a 0.50x downscale
//     info  0.055 charSize * 48 fontSize -> 0.264 units -> 12.1 px, from that same
//           48px atlas: a 0.25x downscale
//
// 12px of bilinear-minified atlas text is the fuzz the player reported. Canvas UI
// in the same screenshot stayed crisp because UIRoot is ScreenSpaceOverlay +
// ScaleWithScreenSize, which rasterises glyphs at their final on-screen size and
// is immune to the camera by construction (see Phase20CameraFramingTests).
//
// So the fix is the RENDERING PATH, not a larger charSize: a bigger TextMesh is
// still minified atlas text, and it would break again on the next zoom change.
// Pinch-zoom is queued, which makes that a certainty rather than a risk. The two
// lines are now `UnityEngine.UI.Text` on UIRoot.Canvas — legacy uGUI, matching
// every other UI surface in the project (HudUI/DialogueUI/QuestLogUI/RegionMapUI
// all use UIRoot.MakeText; TextMeshPro's essential resources are not imported
// into this project, so TMP text would render nothing at runtime).
//
// Only the ANCHOR still reads the camera: the label is positioned each frame by
// projecting the player's head through Camera.main, so it stays pinned over the
// character at any zoom while its type size never changes.
// Phase22LegibilityPathTests pins all of this so it can't regress to world space.
using CosmicBloom.UI;
using CosmicBloom.World;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.Farming
{
    public class FarmActionPrompt : MonoBehaviour
    {
        // Type sizes are in CanvasScaler REFERENCE pixels (UIRoot: 1080x1920 portrait),
        // so they are a fixed fraction of screen height on every device — and unrelated
        // to orthographicSize.
        //
        // Derived, not taste. The floor is HudUI's stats size (26): that is the element
        // confirmed CRISP in the very screenshot that showed this label as fuzzy, so it
        // is the project's own demonstrated legibility floor rather than an imported
        // number. Both lines ship ABOVE it, because unlike the HUD bars this label has
        // no solid backing panel and sits over busy tilemap/crop art — see LegibilityInk
        // below for the other half of that problem.
        public const int VerbFontSize = 40;
        public const int InfoFontSize = 30;

        /// <summary>The floor both sizes are measured against: HudUI's stats font size.</summary>
        public const int LegibilityFloorFontSize = 26;

        /// <summary>Dark outline behind the glyphs. Size alone does not make text over
        /// moving world art readable — the old TextMesh had no contrast treatment either,
        /// and pale text crossing a pale crop sprite disappears at any size. One px of
        /// outline at reference scale is what makes the label independent of whatever is
        /// underneath it.</summary>
        static readonly Color LegibilityInk = new Color(0.03f, 0.02f, 0.08f, 0.9f);

        /// <summary>Character art is 64px at PPU 64 (see Phase20CameraFramingTests), so a
        /// sprite is 1 world unit tall on a centred pivot — its head is +0.5 units.</summary>
        const float PlayerHalfHeightUnits = 0.5f;

        /// <summary>Floor for the gap between the player's centre and the verb baseline,
        /// in canvas reference px. The anchor offset is derived from world space so it
        /// tracks the sprite as zoom changes; this stops a far-zoomed-out view from
        /// collapsing that offset to nothing and printing the label over the character.</summary>
        public const float MinHeadroomPx = 34f;

        /// <summary>Wide enough for the longest CropInfo line; overflow (below) means it
        /// is a layout hint rather than a limit, so nothing is ever clipped.</summary>
        const float BlockWidthPx = 620f;

        Transform _player;
        RectTransform _root;
        RectTransform _canvasRect;
        Canvas _canvas;
        Camera _cam;
        Text _verb;
        Text _info;

        public static FarmActionPrompt Create(Transform player)
        {
            var canvas = UIRoot.Canvas;

            var go = new GameObject("FarmActionPrompt", typeof(RectTransform));
            var root = (RectTransform)go.transform;
            root.SetParent(canvas.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            // Behind every panel: this is a world hint, so the HUD bars, dialogue box and
            // Star Map must always draw over it. GameBootstrap creates the HUD first, which
            // would otherwise put this on top of the bottom button bar.
            root.SetAsFirstSibling();

            // Both lines are full-alpha: the first capture ran them at 0.97 over bright
            // lush-tier ground and the info block visibly washed out, which is the same
            // legibility failure as the wrong size arriving by a different route. The info
            // line keeps only a faint blue cast to stay distinguishable from the verb.
            var verb = MakeLine(root, "Verb", VerbFontSize, Color.white, 0f);
            var info = MakeLine(root, "CropInfo", InfoFontSize, new Color(0.90f, 0.96f, 1f, 1f),
                                VerbFontSize * 1.7f);   // clear of the verb's ascenders

            var prompt = go.AddComponent<FarmActionPrompt>();
            prompt._player = player;
            prompt._canvas = canvas;
            prompt._canvasRect = (RectTransform)canvas.transform;
            prompt._root = root;
            prompt._verb = verb;
            prompt._info = info;
            return prompt;
        }

        /// <summary>One stacked line, bottom-anchored at <paramref name="yPx"/> above the
        /// root so it grows UPWARD, away from the player and anything standing beside them
        /// (the P3-Phase 21 stacking rule, carried over verbatim).</summary>
        static Text MakeLine(RectTransform parent, string name, int size, Color color, float yPx)
        {
            var text = UIRoot.MakeText(parent, name, size, TextAnchor.LowerCenter, FontStyle.Bold);
            text.color = color;
            // NOT resizeTextForBestFit, and that is deliberate. The Phase 6 rule exists
            // because Unity's default verticalOverflow=Truncate silently DROPS real data
            // that outgrows its box. This label has no box to fit inside — so it solves
            // the same problem the other way, by letting text overflow its rect rather
            // than shrink. Best-fit here would re-introduce exactly the failure being
            // fixed: content quietly scaling itself back down below legibility.
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // CropInfo stacks up to three lines; at default leading they crowd into a block
            // that reads as one smear against textured ground.
            text.lineSpacing = 1.18f;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = LegibilityInk;
            outline.effectDistance = new Vector2(2f, 2f);
            // MakeText already sets raycastTarget=false; restated because it is load-bearing
            // here — a Graphic over the play area would eat the ground taps this very label
            // is telling the player to make.
            text.raycastTarget = false;

            var rt = (RectTransform)text.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(BlockWidthPx, size * 1.4f);
            rt.anchoredPosition = new Vector2(0f, yPx);
            return text;
        }

        void LateUpdate()
        {
            if (_player == null) { Destroy(gameObject); return; }
            if (_verb == null || _canvas == null) return;

            // Content first, and unconditionally: it must not depend on a camera existing.
            string label = null;
            string cropLines = null;
            var regionId = RegionManager.Instance != null ? RegionManager.Instance.CurrentRegionId : null;
            var grid = regionId != null ? FarmGrid.ForRegion(regionId) : null;
            if (grid != null)
            {
                label = LabelFor(grid.ContextActionAt(_player.position));
                // The facts come from CropInfo (crops.json + live PlotState) via the grid
                // that already owns this region's state — never assembled here.
                cropLines = grid.CropInfoAt(_player.position).WorldLines;
            }

            _verb.text = label ?? string.Empty;
            if (_info != null) _info.text = cropLines ?? string.Empty;

            FollowPlayer();
        }

        /// <summary>Project the player's head to canvas space. This is the ONLY place the
        /// camera is read, and it moves the anchor only — never the type size.</summary>
        void FollowPlayer()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || _canvasRect == null)
            {
                // No camera (several PlayMode tests spawn none): keep updating the text,
                // just leave the label parked. Nothing to project onto.
                SetVisible(true);
                return;
            }

            var basePoint = _cam.WorldToScreenPoint(_player.position);
            if (basePoint.z < 0f) { SetVisible(false); return; }   // behind the camera
            SetVisible(true);

            var headPoint = _cam.WorldToScreenPoint(_player.position + Vector3.up * PlayerHalfHeightUnits);
            var uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, basePoint, uiCam, out var baseLocal))
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, headPoint, uiCam, out var headLocal);

            float headroom = Mathf.Max(headLocal.y - baseLocal.y, MinHeadroomPx);
            _root.anchoredPosition = new Vector2(baseLocal.x, baseLocal.y + headroom);
        }

        void SetVisible(bool on)
        {
            if (_verb != null && _verb.enabled != on) _verb.enabled = on;
            if (_info != null && _info.enabled != on) _info.enabled = on;
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
        public string CurrentLabel => _verb != null && !string.IsNullOrEmpty(_verb.text) ? _verb.text : null;

        /// <summary>Test/inspection access — the crop-info line, null when no crop.</summary>
        public string CurrentCropInfo =>
            _info != null && !string.IsNullOrEmpty(_info.text) ? _info.text : null;

        /// <summary>Test/inspection access to the actual rendering path, so a regression
        /// back to world-space TextMesh is a failing assert rather than a live discovery.
        /// </summary>
        public Graphic VerbGraphic => _verb;
        public Graphic InfoGraphic => _info;

        /// <summary>Test/inspection access — the canvas-space anchor driven by
        /// <see cref="FollowPlayer"/>. Its position tracks zoom; the type size does not.</summary>
        public RectTransform Anchor => _root;
    }
}
