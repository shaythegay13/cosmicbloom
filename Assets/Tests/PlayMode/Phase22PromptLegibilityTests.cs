// P3-Phase 22 verification — the player-following context prompt renders through
// Canvas UI, not world-space TextMesh.
//
// The bug: P3-Phase 17 built the verb line and P3-Phase 21 the crop-info line as
// world-space `TextMesh`. That ties glyph size to `orthographicSize`, so
// P3-Phase 20's framing retune (ortho 7 -> 14) silently halved both, leaving the
// info line at ~12 screen px minified 0.25x out of a 48px font atlas — fuzzy in a
// direct screenshot, while Canvas UI in the SAME shot stayed crisp.
//
// These tests exist because "make the font bigger" would have looked like a fix
// and regressed on the next zoom change. What has to hold is the RENDERING PATH:
//   1. PromptRendersThroughCanvasUi_NeverWorldSpaceTextMesh — the anti-regression
//      guard. No TextMesh/MeshRenderer anywhere in the prompt; both lines are uGUI
//      Graphics parented to UIRoot's ScreenSpaceOverlay canvas.
//   2. PromptTypeSize_IsInvariantAcrossCameraZoom — read off the live component at
//      three ortho sizes, with the old world-space math computed alongside to show
//      what it used to do. This is the property pinch-zoom depends on.
//   3. PromptTracksThePlayerOnScreen_AtEveryZoom — the anchor still follows.
//   4. PromptNeitherBlocksWorldTapsNorCoversHud — a Graphic over the play area must
//      not eat the taps it is telling the player to make.
//   5. PromptStillReportsRealVerbAndCropInfo — the P17/P21 content contracts survive
//      the move.
//   6. Phase22_PromptLegibility_Screenshots — the visual evidence. Renders a camera,
//      so it belongs in the RENDER split, never the -nographics filter.
using System.Collections;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CosmicBloom.Tests
{
    public class Phase22PromptLegibilityTests
    {
        // Same portrait capture size the rest of the suite uses (Phase20CameraFramingTests).
        const int W = 720, H = 1280;
        const string Vale = "starlight_vale";

        RegionManager _regions;
        System.Collections.Generic.Dictionary<string, Grid> _grids;
        GameState _state;
        FarmingSystem _farming;
        PlayerController _player;
        FarmActionPrompt _prompt;
        Camera _cam;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _regions.SetCurrentRegion(Vale);
            _state = new GameState(new SaveSnapshot());
            _state.Save.currentRegion = Vale;
            _farming = new FarmingSystem(_state);
            FarmGrid.Create(Vale, _grids[Vale], _farming, _state);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            if (_prompt != null) Object.DestroyImmediate(_prompt.gameObject);
            if (_player != null) Object.DestroyImmediate(_player.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
        }

        /// <summary>Player at the centre of a lush cell, with the prompt attached.</summary>
        void SpawnPlayerWithPrompt(Vector3 at)
        {
            _player = PlayerController.Spawn(at);
            _prompt = FarmActionPrompt.Create(_player.transform);
        }

        Camera MakeCamera(float orthoSize)
        {
            var go = new GameObject("PromptCam") { tag = "MainCamera" };
            _cam = go.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = orthoSize;
            _cam.aspect = (float)W / H;
            _cam.transform.position = new Vector3(_player.transform.position.x, _player.transform.position.y, -10f);
            _cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            return _cam;
        }

        /// <summary>What the PRE-Phase-22 world-space TextMesh rendered at, in screen px on
        /// an H-tall frame. A TextMesh glyph is `fontSize * characterSize * 0.1` world units
        /// tall, and there are `H / (2 * orthoSize)` screen px per world unit.</summary>
        static float OldWorldSpacePx(float characterSize, float orthoSize)
            => characterSize * 48f * 0.1f * (H / (2f * orthoSize));

        // ── 1. the anti-regression guard ─────────────────────────────────────────
        [Test]
        public void PromptRendersThroughCanvasUi_NeverWorldSpaceTextMesh()
        {
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));

            // The failure this pins: any future phase re-adding a world-space text
            // component here re-couples legibility to camera zoom.
            Assert.IsEmpty(_prompt.GetComponentsInChildren<TextMesh>(true),
                "no world-space TextMesh anywhere in the prompt — that is the bug being fixed");
            Assert.IsEmpty(_prompt.GetComponentsInChildren<MeshRenderer>(true),
                "no MeshRenderer either: world-space text draws through one, Canvas UI does not");

            // Both lines are real uGUI Graphics.
            Assert.IsInstanceOf<Text>(_prompt.VerbGraphic, "verb line is a Canvas Text");
            Assert.IsInstanceOf<Text>(_prompt.InfoGraphic, "crop-info line is a Canvas Text");
            Assert.IsNotNull(_prompt.VerbGraphic.canvasRenderer,
                "and it draws through a CanvasRenderer, the Canvas batching path");

            // Parented to the one screen-space canvas, which is what makes it zoom-immune.
            var canvas = _prompt.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "prompt lives under a Canvas");
            Assert.AreEqual(UIRoot.Canvas, canvas, "specifically UIRoot's canvas, not a private one");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
                "ScreenSpaceOverlay — rasterises at final screen size, immune to the camera");

            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode,
                "so type size is a fixed fraction of screen height on every device");

            // Same font family as the rest of the UI — this is a UI element now, not world art.
            var hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Assert.AreEqual(hudFont, ((Text)_prompt.VerbGraphic).font, "matches UIRoot.MakeText's font");
            Debug.Log("[P22] prompt renders via UnityEngine.UI.Text on UIRoot's ScreenSpaceOverlay canvas; " +
                      "zero TextMesh/MeshRenderer components remain");
        }

        // ── 2. the property pinch-zoom depends on ────────────────────────────────
        [UnityTest]
        public IEnumerator PromptTypeSize_IsInvariantAcrossCameraZoom()
        {
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));
            MakeCamera(CameraFollow.DefaultOrthographicSize);
            yield return null;

            var verb = (Text)_prompt.VerbGraphic;
            var info = (Text)_prompt.InfoGraphic;
            var canvas = UIRoot.Canvas;

            // Sweep the zoom range a pinch gesture would plausibly cover, including the
            // pre-Phase-20 value that caused this regression in the first place.
            float[] zooms = { 7f, CameraFollow.DefaultOrthographicSize, 28f };
            var verbSizes = new System.Collections.Generic.List<int>();
            var infoSizes = new System.Collections.Generic.List<int>();

            foreach (var z in zooms)
            {
                _cam.orthographicSize = z;
                yield return null;   // a full LateUpdate at this zoom
                verbSizes.Add(verb.fontSize);
                infoSizes.Add(info.fontSize);
                Debug.Log($"[P22] ortho {z,5}: verb {verb.fontSize}px / info {info.fontSize}px (reference px, unchanged) " +
                          $"— the OLD world-space text would have been " +
                          $"{OldWorldSpacePx(0.11f, z):F1}px / {OldWorldSpacePx(0.055f, z):F1}px on a {H}px frame");
            }

            Assert.AreEqual(1, verbSizes.Distinct().Count(),
                $"verb type size is identical at every zoom; saw [{string.Join(", ", verbSizes)}]");
            Assert.AreEqual(1, infoSizes.Distinct().Count(),
                $"info type size is identical at every zoom; saw [{string.Join(", ", infoSizes)}]");

            // Regression pin on the OLD behaviour, so the contrast is on the record and
            // nobody re-derives world-space text as "equivalent".
            Assert.AreNotEqual(OldWorldSpacePx(0.055f, 7f), OldWorldSpacePx(0.055f, 28f),
                "the old world-space info line DID change size with zoom — 4x across this sweep");
            Assert.Less(OldWorldSpacePx(0.055f, CameraFollow.DefaultOrthographicSize), 13f,
                "regression pin: at the shipped ortho 14 the old info line was ~12px of minified atlas text");

            // And both lines clear the project's own demonstrated legibility floor.
            Assert.GreaterOrEqual(info.fontSize, FarmActionPrompt.LegibilityFloorFontSize,
                "info line is at least HudUI's stats size — the element confirmed crisp in the same screenshot");
            Assert.Greater(verb.fontSize, info.fontSize, "the verb is the primary call to action");

            // NB: canvas.scaleFactor here reflects the tiny batchmode Screen, NOT a device.
            // Reference px are what ship: at the 1080x1920 reference the scale factor is 1,
            // so `fontSize` IS the device pixel height. Quote the reference number.
            Debug.Log($"[P22] info line = {info.fontSize} reference px (= {info.fontSize} device px at the " +
                      $"1080x1920 CanvasScaler reference; {info.fontSize * 0.667f:F0} px on a 720x1280 capture). " +
                      $"Floor is HudUI's {FarmActionPrompt.LegibilityFloorFontSize}. " +
                      $"Batchmode Screen scaleFactor was {canvas.scaleFactor:F3} — a harness artifact, not a device value.");
        }

        // ── 3. the anchor still follows the player ───────────────────────────────
        [UnityTest]
        public IEnumerator PromptTracksThePlayerOnScreen_AtEveryZoom()
        {
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));
            MakeCamera(CameraFollow.DefaultOrthographicSize);
            yield return null;

            var canvasRect = (RectTransform)UIRoot.Canvas.transform;

            // The camera is PINNED at the region centre and the player walks around inside
            // the frame. Re-centring the camera on the player each step (what CameraFollow
            // does) would put the player at canvas (0,0) every time and make a horizontal
            // tracking assertion pass without tracking anything.
            var camAt = new Vector3(20f, 20f, -10f);

            foreach (var z in new[] { 7f, CameraFollow.DefaultOrthographicSize, 28f })
            {
                _cam.orthographicSize = z;
                _cam.transform.position = camAt;

                var seenX = new System.Collections.Generic.List<float>();
                foreach (var pos in new[]
                {
                    new Vector3(20f, 20f, 0f),      // dead centre
                    new Vector3(23.5f, 21.5f, 0f),  // right of centre
                    new Vector3(16.5f, 18.5f, 0f),  // left of centre
                })
                {
                    _player.transform.position = pos;
                    yield return null;

                    // Where the player actually is on screen, in canvas-local units.
                    var screenPt = _cam.WorldToScreenPoint(pos);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, screenPt, null, out var playerLocal);

                    var anchored = _prompt.Anchor.anchoredPosition;
                    Assert.AreEqual(playerLocal.x, anchored.x, 1.0f,
                        $"label is horizontally centred on the player at ortho {z}, pos {pos}");

                    float headroom = anchored.y - playerLocal.y;
                    Assert.GreaterOrEqual(headroom, FarmActionPrompt.MinHeadroomPx - 0.01f,
                        $"label clears the player's head at ortho {z} (headroom {headroom:F1}px) — the floor " +
                        "stops a zoomed-out view from printing it over the character");
                    seenX.Add(anchored.x);
                    Debug.Log($"[P22] ortho {z,5} pos {pos}: label anchored at {anchored}, " +
                              $"player at {playerLocal}, headroom {headroom:F1} canvas px");
                }

                // Proof the label really moved, rather than sitting at a fixed spot that
                // happened to match a re-centred player.
                Assert.AreEqual(3, seenX.Distinct().Count(),
                    $"the label tracked to 3 distinct horizontal positions at ortho {z}; saw " +
                    $"[{string.Join(", ", seenX.Select(v => v.ToString("F1")))}]");
                Assert.Greater(seenX[1], seenX[0], "player right of centre -> label right of centre");
                Assert.Less(seenX[2], seenX[0], "player left of centre -> label left of centre");
            }
        }

        // ── 4. it must not eat taps, or cover the HUD ────────────────────────────
        [Test]
        public void PromptNeitherBlocksWorldTapsNorCoversHud()
        {
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));

            foreach (var g in new[] { _prompt.VerbGraphic, _prompt.InfoGraphic })
                Assert.IsFalse(g.raycastTarget,
                    $"{g.gameObject.name} must not raycast — a Graphic over the play area would eat the " +
                    "very ground taps this label is prompting for");

            Assert.IsNull(_prompt.GetComponent<Graphic>(),
                "the root is a bare RectTransform, so it cannot block taps either");

            // Drawn beneath every panel: GameBootstrap builds the HUD first, so without an
            // explicit reorder this would sit on top of the bottom button bar.
            Assert.AreEqual(0, _prompt.transform.GetSiblingIndex(),
                "prompt is the first canvas sibling, so HUD bars / dialogue / Star Map draw over it");
            Debug.Log("[P22] prompt is non-raycasting and sits at canvas sibling index 0 (behind all HUD panels)");
        }

        // ── 5. the P17 / P21 content contracts survived the move ─────────────────
        [UnityTest]
        public IEnumerator PromptStillReportsRealVerbAndCropInfo()
        {
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));
            yield return null;

            Assert.AreEqual("Tap to till", _prompt.CurrentLabel, "P17 verb contract, now via Canvas Text");
            Assert.IsNull(_prompt.CurrentCropInfo, "no crop under the player yet");

            var crop = GameDatabase.Crops.First(c => c.region == Vale);
            Assert.IsTrue(_farming.Till(14, 14));
            _state.Save.seeds[crop.id] = 5;
            Assert.IsTrue(_farming.Plant(14, 14, crop.id));
            yield return null;

            Assert.AreEqual("Tap to water", _prompt.CurrentLabel, "freshly planted crop wants its first watering");
            Assert.IsNotNull(_prompt.CurrentCropInfo, "P21 crop-info contract still holds");
            StringAssert.Contains(crop.name, _prompt.CurrentCropInfo);
            StringAssert.Contains("stage 1/", _prompt.CurrentCropInfo);

            // The accessors read the real Text components, not a cached string.
            Assert.AreEqual(((Text)_prompt.VerbGraphic).text, _prompt.CurrentLabel);
            Assert.AreEqual(((Text)_prompt.InfoGraphic).text, _prompt.CurrentCropInfo);

            // Content is camera-independent: these assertions ran with no camera at all.
            Assert.IsNull(Camera.main, "this test deliberately has no camera — text still updated");
            Debug.Log($"[P22] verb '{_prompt.CurrentLabel}' / info '{_prompt.CurrentCropInfo}' " +
                      "read straight off the Canvas Text components, with no camera present");
        }

        // ── 6. visual evidence (RENDER split) ────────────────────────────────────
        [UnityTest]
        public IEnumerator Phase22_PromptLegibility_Screenshots()
        {
            var state = _state;
            state.AddCoins(5000);
            var engine = _regions.gameObject.AddComponent<QuestEngine>();
            engine.Bind(state);
            DialogueUI.Close();   // q_arrival's cutscene auto-opens on Bind

            var hud = HudUI.Create(state, engine, _regions);
            SpawnPlayerWithPrompt(new Vector3(14.5f, 14.5f, 0f));

            // A planted, unwatered crop under the player: verb line AND info line both live.
            var crop = GameDatabase.Crops.First(c => c.region == Vale);
            _farming.Till(14, 14);
            state.Save.seeds[crop.id] = 5;
            _farming.Plant(14, 14, crop.id);

            MakeCamera(CameraFollow.DefaultOrthographicSize);

            // An Overlay canvas does not render into a RenderTexture, so the capture forces
            // ScreenSpaceCamera. The prompt reads renderMode when projecting, so it follows
            // correctly either way — and in the shipping build the canvas stays Overlay.
            var canvas = UIRoot.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;

            try
            {
                DialogueUI.Close();
                yield return null;

                // The money shot: prompt + HUD in ONE frame at the shipped ortho 14, which
                // is exactly the comparison the bug report was made from (HUD crisp,
                // prompt fuzzy). Both must now read as crisp.
                yield return CaptureCanvas("phase22_prompt_at_ortho14.png");

                var info = (Text)_prompt.InfoGraphic;
                Debug.Log($"[P22] captured at ortho {_cam.orthographicSize}: canvas scaleFactor " +
                          $"{canvas.scaleFactor:F3}, info line {info.fontSize} reference px -> " +
                          $"{info.fontSize * canvas.scaleFactor:F1} px on this {W}x{H} frame " +
                          $"(the old world-space line was {OldWorldSpacePx(0.055f, _cam.orthographicSize):F1}px)");

                // Zoomed out 2x — the pinch-zoom proof. World art halves; the label must not.
                _cam.orthographicSize = 28f;
                yield return null;
                yield return CaptureCanvas("phase22_prompt_at_ortho28_zoomed_out.png");
                Debug.Log($"[P22] captured at ortho 28: info line still {info.fontSize} reference px " +
                          $"(old world-space equivalent: {OldWorldSpacePx(0.055f, 28f):F1}px)");
            }
            finally
            {
                Object.DestroyImmediate(hud.QuestLog.gameObject);
                Object.DestroyImmediate(hud.Map.gameObject);
                Object.DestroyImmediate(hud.Pause.gameObject);
                Object.DestroyImmediate(hud.Ranch.gameObject);
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        /// <summary>Capture with the RenderTexture BOUND ACROSS A FRAME, not just for the
        /// Render() call. A ScreenSpaceCamera canvas sizes itself to the camera's pixelRect,
        /// which only becomes W x H once targetTexture is assigned — binding it just before
        /// Render() would lay the UI out against the batchmode Screen size and capture text
        /// at the wrong scale, which is precisely the kind of misleading evidence this
        /// phase exists to eliminate.</summary>
        IEnumerator CaptureCanvas(string fileName)
        {
            var rt = new RenderTexture(W, H, 24);
            _cam.targetTexture = rt;
            _cam.aspect = (float)W / H;
            yield return null;   // canvas re-lays-out against the RT
            yield return null;   // prompt's LateUpdate projects against it

            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;

            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", fileName), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
