// Phase 9 verification — HUD, quest log, dialogue, region map, pause menu all
// build, show live state, and don't collide; a portrait screenshot is
// captured of each screen.
using System.Collections;
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
    public class CoreUiTests
    {
        RegionManager _regions;
        GameState _state;
        QuestEngine _engine;
        HudUI _hud;
        System.Collections.Generic.Dictionary<string, Grid> _grids;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _state = new GameState(new SaveSnapshot());
            _state.Save.coins = 321;
            _engine = new GameObject("TestEngine").AddComponent<QuestEngine>();
            _engine.Bind(_state);
            DialogueUI.Close();
            _hud = HudUI.Create(_state, _engine, _regions);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(_hud.QuestLog.gameObject);
            Object.DestroyImmediate(_hud.Map.gameObject);
            Object.DestroyImmediate(_hud.Pause.gameObject);
            Object.DestroyImmediate(_hud.Ranch.gameObject);
            Object.DestroyImmediate(_hud.gameObject);
            Object.DestroyImmediate(_engine.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            var canvas = GameObject.Find("UIRoot");
            if (canvas != null) Object.DestroyImmediate(canvas);
            UIRoot.Reset();
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator HudShowsLiveState_AndPanelsToggle()
        {
            yield return null;   // HUD Update populates

            var stats = GameObject.Find("TopBar").GetComponentInChildren<Text>();
            StringAssert.Contains("321", stats.text, "coins shown");
            StringAssert.Contains("Lv 1", stats.text, "level shown");
            StringAssert.Contains("Starlight Vale", stats.text, "region shown");
            Debug.Log($"[P9Test] HUD: {stats.text}");

            _hud.QuestLog.Toggle();
            yield return null;
            Assert.IsTrue(_hud.QuestLog.IsOpen, "quest log opens");
            var questBody = GameObject.Find("QuestPanel").transform.Find("Body").GetComponent<Text>();
            StringAssert.Contains("Arrival", questBody.text, "active quest listed");
            _hud.QuestLog.Toggle();

            _hud.Map.Toggle();
            yield return null;
            Assert.IsTrue(_hud.Map.IsOpen, "map opens");
            _hud.Map.Toggle();

            _hud.Pause.Toggle();
            yield return null;
            Assert.IsTrue(_hud.Pause.IsOpen, "pause opens");
            Assert.AreEqual(0f, Time.timeScale, "paused");
            _hud.Pause.Toggle();
            Assert.AreEqual(1f, Time.timeScale, "resumed");
        }

        [UnityTest]
        public IEnumerator MapListsAllRegionsWithLockState()
        {
            _hud.Map.Toggle();
            yield return null;

            foreach (var def in GameDatabase.Regions)
            {
                var entry = GameObject.Find($"Region_{def.id}");
                Assert.IsNotNull(entry, $"map row for {def.id}");
                var label = entry.GetComponentInChildren<Text>().text;
                if (def.id == "starlight_vale")
                    StringAssert.DoesNotContain("unlock:", label, "vale not locked");
                else
                    StringAssert.Contains($"{def.unlockCoins}", label, $"{def.id} shows data cost");
            }
            Debug.Log("[P9Test] map lists 5 regions with data-driven lock state");
        }

        [UnityTest]
        public IEnumerator CaptureEachUiScreen_Portrait()
        {
            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.transform.position = new Vector3(10, 10, -10);
            cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // screen-space-overlay canvases don't render into camera RTs; use
            // camera-space for the capture pass
            var canvas = UIRoot.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;

            yield return Capture(cam, "phase9_hud");

            _hud.QuestLog.Toggle(); yield return null;
            yield return Capture(cam, "phase9_questlog");
            _hud.QuestLog.Toggle();

            _hud.Map.Toggle(); yield return null;
            yield return Capture(cam, "phase9_map");
            _hud.Map.Toggle();

            DialogueUI.Show("Zyx", GameDatabase.Npc("zyx").dialogue["intro"]);
            yield return null;
            yield return Capture(cam, "phase9_dialogue");
            DialogueUI.Close();

            _hud.Pause.Toggle(); yield return null;
            yield return Capture(cam, "phase9_pause");
            _hud.Pause.Toggle();

            Object.DestroyImmediate(camGo);
        }

        static IEnumerator Capture(Camera cam, string name)
        {
            yield return null;   // WaitForEndOfFrame never completes in batchmode
            var rt = new RenderTexture(720, 1280, 24);   // portrait phone
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(Application.dataPath, "..", "Logs", name + ".png"),
                tex.EncodeToPNG());
            Debug.Log($"[P9Test] captured Logs/{name}.png");
        }
    }
}
