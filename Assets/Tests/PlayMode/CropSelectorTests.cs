// Crop-selection investigation (2026-07-25): confirms CropSelection.cs +
// HudUI's "Crop" bottom-bar button (built Phase 3/9) already let a player
// plant 2+ different crops in the SAME region through real interaction — no
// new selection mechanism or crop data was needed. This test drives the real
// entry points: PlayerController.OnTapWorld for till/plant (same precedent as
// Phase17LiveVerifyTests) and a genuine simulated touchscreen tap on the HUD's
// actual Crop button (through EventSystem/InputSystemUIInputModule, not a
// direct method call) to cycle the region's selected crop.
using System.Collections;
using System.IO;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CosmicBloom.Tests
{
    public class CropSelectorTests : InputTestFixture
    {
        RegionManager _regions;
        System.Collections.Generic.Dictionary<string, Grid> _grids;
        GameState _state;
        FarmingSystem _farming;
        QuestEngine _engine;
        PlayerController _player;
        HudUI _hud;

        public override void Setup()
        {
            base.Setup();
            // UIRoot's EventSystem is DontDestroyOnLoad and shared across the whole test
            // run. If an earlier, non-InputTestFixture test created it first, its
            // InputSystemUIInputModule is bound to the pre-isolation Input System and
            // goes stale the moment base.Setup() above swaps in this test's isolated
            // input environment — simulated touches then silently never reach the UI
            // raycaster. Force a fresh EventSystem so UIRoot.Canvas recreates one inside
            // THIS test's isolated environment (matches how it behaves when run alone).
            var existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (existingEventSystem != null) Object.DestroyImmediate(existingEventSystem.gameObject);
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _state = new GameState(new SaveSnapshot());
            CropSelection.GrantStarterSeeds(_state);
            _state.Save.coins = 200;   // enough to auto-buy a second crop's seed
            _farming = new FarmingSystem(_state);
            _engine = _regions.gameObject.AddComponent<QuestEngine>();
            _engine.Bind(_state);
            FarmGrid.Create("starlight_vale", _grids["starlight_vale"], _farming, _state);
            _player = PlayerController.Spawn(new Vector3(20, 20));
            _hud = HudUI.Create(_state, _engine, _regions);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_hud.QuestLog.gameObject);
            Object.DestroyImmediate(_hud.Map.gameObject);
            Object.DestroyImmediate(_hud.Pause.gameObject);
            Object.DestroyImmediate(_hud.Ranch.gameObject);
            Object.DestroyImmediate(_hud.gameObject);
            // _engine shares its GameObject with _regions (AddComponent in Setup) —
            // destroying _regions.gameObject below covers it; a separate destroy here
            // would throw MissingReferenceException on the second attempt.
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
            base.TearDown();
        }

        void TillAndPlantViaRealTap(int x, int y)
        {
            var tile = new Vector3(x + 0.5f, y + 0.5f, 0f);
            _player.OnTapWorld(tile);   // till (walks there first if out of range)
            for (int i = 0; i < 2000 && _player.MoveTarget != null; i++) _player.MoveStep(0.05f);
            Assert.IsNotNull(_farming.PlotAt(x, y), $"({x},{y}) tilled");
            Assert.IsTrue(_farming.PlotAt(x, y).tilled);

            _player.OnTapWorld(tile);   // plant the region's currently-selected crop
        }

        [UnityTest]
        public IEnumerator TwoDifferentCropsPlantableInSameRegion_ViaRealCropButtonTap()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;   // HUD Update populates the Crop label

            // ── plot A: plant whatever the region defaults to ──
            const int Ax = 4, Ay = 4;
            string cropA = CropSelection.SelectedFor(_state, "starlight_vale");
            TillAndPlantViaRealTap(Ax, Ay);
            yield return new WaitForFixedUpdate();
            var plotA = _farming.PlotAt(Ax, Ay);
            Assert.IsNotNull(plotA.crop, "plot A planted");
            Assert.AreEqual(cropA, plotA.crop.id, "plot A holds the region's default-selected crop");
            Debug.Log($"[CropSelectorTest] plot A ({Ax},{Ay}) planted default crop '{cropA}'");

            // ── cycle the selection via a REAL simulated touch on the HUD's Crop button ──
            var cropButtonGo = GameObject.Find("Crop");
            Assert.IsNotNull(cropButtonGo, "HUD Crop button exists in the bottom bar");
            var cropButton = cropButtonGo.GetComponent<Button>();
            var cropLabel = cropButtonGo.GetComponentInChildren<Text>();
            string labelBefore = cropLabel.text;

            Vector3 buttonScreenPos = cropButtonGo.transform.position;   // Screen Space Overlay: world pos == screen px
            BeginTouch(1, new Vector2(buttonScreenPos.x, buttonScreenPos.y), screen: touchscreen);
            yield return null;
            EndTouch(1, new Vector2(buttonScreenPos.x, buttonScreenPos.y), screen: touchscreen);
            yield return null;
            yield return null;

            string cropB = CropSelection.SelectedFor(_state, "starlight_vale");
            Debug.Log($"[CropSelectorTest] real touch on Crop button: label '{labelBefore}' -> '{cropLabel.text}', " +
                      $"selection '{cropA}' -> '{cropB}'");
            Assert.AreNotEqual(cropA, cropB, "a real tap on the Crop button changed the region's selected crop");
            Assert.AreNotEqual(labelBefore, cropLabel.text, "the Crop button's own label updated from the real tap");
            // P3-Phase 21 reformatted this label to read as a control ("Crop: X ⇄")
            // rather than a bare status readout, so match on containment, not equality.
            StringAssert.Contains(GameDatabase.Crop(cropB)?.name, cropLabel.text,
                "label shows the newly-selected crop's data name");
            StringAssert.StartsWith("Crop:", cropLabel.text, "label reads as a control, not a status readout");

            // ── plot B: same region, now plants the newly-selected crop ──
            const int Bx = 4, By = 6;
            TillAndPlantViaRealTap(Bx, By);
            yield return new WaitForFixedUpdate();
            var plotB = _farming.PlotAt(Bx, By);
            Assert.IsNotNull(plotB.crop, "plot B planted");
            Assert.AreEqual(cropB, plotB.crop.id, "plot B holds the newly-selected crop");
            Debug.Log($"[CropSelectorTest] plot B ({Bx},{By}) planted cycled crop '{cropB}'");

            Assert.AreNotEqual(plotA.crop.id, plotB.crop.id,
                "SAME region, 2 different plots, 2 different crops — proven through real tap interaction");

            // ── screenshot: HUD showing the Crop button after the real cycle ──
            DialogueUI.Close();   // q_arrival's intro auto-opens on QuestEngine.Bind; don't let it hide the shot
            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.transform.position = new Vector3(4.5f, 5f, -10);   // frames both planted plots (4,4) and (4,6)
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            var canvas = UIRoot.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            yield return null;

            const int w = 720, h = 1280;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", "crop_selector_after_cycle.png"),
                               tex.EncodeToPNG());
            Debug.Log("[CropSelectorTest] captured Logs/crop_selector_after_cycle.png");
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }
}
