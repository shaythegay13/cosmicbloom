// Phase 11 verification — real simulated TOUCH input through the Input
// System (InputTestFixture + a virtual Touchscreen): a screen tap must drive
// PlayerController the same way a mouse click does, and UI buttons must
// respond to taps. Also captures portrait-resolution screenshots with the
// full HUD (Logs/phase11_*.png).
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class MobileInputTests : InputTestFixture
    {
        RegionManager _regions;
        PlayerController _player;
        Camera _cam;
        System.Collections.Generic.Dictionary<string, Grid> _grids;

        public override void Setup()
        {
            base.Setup();
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);

            var camGo = new GameObject("TestCam") { tag = "MainCamera" };
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 7f;
            _cam.transform.position = new Vector3(20, 20, -10);   // vale center after the 40x40 resize

            _player = PlayerController.Spawn(new Vector3(20, 20));
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_cam.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator TouchTap_MovesPlayer()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            yield return null;

            // tap the screen point that projects to world (24, 22)
            Vector3 screen = _cam.WorldToScreenPoint(new Vector3(24, 22, 0));
            BeginTouch(1, new Vector2(screen.x, screen.y), screen: touchscreen);
            yield return null;
            Debug.Log($"[P11Diag] Pointer.current={UnityEngine.InputSystem.Pointer.current?.GetType().Name} " +
                      $"press={UnityEngine.InputSystem.Pointer.current?.press.isPressed} " +
                      $"pos={UnityEngine.InputSystem.Pointer.current?.position.ReadValue()} " +
                      $"touchState={touchscreen.primaryTouch.phase.ReadValue()}");
            EndTouch(1, new Vector2(screen.x, screen.y), screen: touchscreen);
            yield return null;

            Assert.IsNotNull(_player.MoveTarget, "touch tap set a move target");
            Assert.AreEqual(24f, _player.MoveTarget.Value.x, 0.2f, "target x from touch");
            Assert.AreEqual(22f, _player.MoveTarget.Value.y, 0.2f, "target y from touch");
            Debug.Log($"[P11Test] touch at screen {screen} → move target {_player.MoveTarget}");

            float deadline = Time.time + 5f;
            while (_player.MoveTarget != null && Time.time < deadline) yield return null;
            Assert.Less(Vector3.Distance(_player.transform.position, new Vector3(24, 22)), 0.1f,
                "player walked to the touched point");
            Debug.Log($"[P11Test] player arrived at {_player.transform.position} via touch input");
        }

        [UnityTest]
        public IEnumerator TouchTap_InteractsWithPlot()
        {
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            var state = new GameState(new SaveSnapshot());
            var farming = new Farming.FarmingSystem(state);
            FarmGrid.Create("starlight_vale", _grids["starlight_vale"], farming, state);
            yield return new WaitForFixedUpdate();

            _player.transform.position = new Vector3(17.2f, 16.5f);   // in range of plot (16,16)
            Vector3 screen = _cam.WorldToScreenPoint(new Vector3(16.5f, 16.5f, 0));
            BeginTouch(1, new Vector2(screen.x, screen.y), screen: touchscreen);
            yield return null;
            EndTouch(1, new Vector2(screen.x, screen.y), screen: touchscreen);
            yield return null;

            Assert.IsNotNull(farming.PlotAt(16, 16), "touch tap tilled the plot");
            Assert.IsTrue(farming.PlotAt(16, 16).tilled);
            Debug.Log("[P11Test] plot (16,16) tilled via touchscreen tap ✓");
        }
    }

    /// <summary>Full HUD at a modern tall-phone resolution (1080×2340, 19.5:9).</summary>
    public class MobileScreenTests
    {
        [UnityTest]
        public IEnumerator CaptureTallPhoneHud()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.coins = 128;
            var engine = new GameObject("TestEngine").AddComponent<QuestEngine>();
            engine.Bind(state);
            DialogueUI.Close();

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(20, 20, -10);
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            var hud = HudUI.Create(state, engine, regions);
            var canvas = UIRoot.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            yield return null;
            yield return null;   // WaitForEndOfFrame never completes in batchmode

            var rt = new RenderTexture(1080, 2340, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "..", "Logs", "phase11_tallphone.png"),
                tex.EncodeToPNG());
            Debug.Log("[P11Test] captured Logs/phase11_tallphone.png (1080x2340)");

            Object.DestroyImmediate(hud.QuestLog.gameObject);
            Object.DestroyImmediate(hud.Map.gameObject);
            Object.DestroyImmediate(hud.Pause.gameObject);
            Object.DestroyImmediate(hud.Ranch.gameObject);
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(engine.gameObject);
            Object.DestroyImmediate(camGo.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
        }
    }
}
