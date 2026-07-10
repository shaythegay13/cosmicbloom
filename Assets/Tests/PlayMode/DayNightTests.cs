// Phase 7 verification — the day/night cycle visibly shifts lighting and
// actually gates crop growth per crops.json growsAt windows. Also captures
// noon vs midnight screenshots of the same region.
using System.Collections;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class DayNightTests
    {
        TimeManager _time;

        [SetUp]
        public void SetUp()
        {
            GameDatabase.LoadAll();
            _time = new GameObject("TestTime").AddComponent<TimeManager>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_time.gameObject);

        static void AgeCropOneStage(FarmingSystem farming, GameState state, int x, int y)
        {
            var plot = farming.PlotAt(x, y);
            var def = GameDatabase.Crop(plot.crop.id);
            // just past one stage — NOT past the 2x offline-catchup override
            plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                   - def.GrowMsPerStage - 1;
        }

        [Test]
        public void NightCrop_OnlyGrowsAtNight()
        {
            var nightCrop = GameDatabase.Crops.First(c => c.growsAt == "night");
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = nightCrop.region;
            var farming = new FarmingSystem(state);
            state.Save.seeds[nightCrop.id] = 1;

            farming.Till(8, 8);
            Assert.IsTrue(farming.Plant(8, 8, nightCrop.id), $"plant {nightCrop.id} in {nightCrop.region}");
            farming.Water(8, 8);
            var plot = farming.PlotAt(8, 8);

            _time.timeOfDay = 0.5f;   // noon
            Assert.IsTrue(_time.IsDay);
            AgeCropOneStage(farming, state, 8, 8);
            farming.FastForward();
            Assert.AreEqual(0, plot.crop.stage, $"{nightCrop.id} (growsAt=night) must NOT advance at noon");

            _time.timeOfDay = 0.9f;   // deep night
            Assert.IsTrue(_time.IsNight);
            farming.FastForward();
            Assert.AreEqual(1, plot.crop.stage, $"{nightCrop.id} advances at night");
            Debug.Log($"[P7Test] {nightCrop.id}: gated at noon, grew at night ✓");
        }

        [Test]
        public void DayCrop_OnlyGrowsByDay()
        {
            var dayCrop = GameDatabase.Crops.First(c => c.growsAt == "day");
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = dayCrop.region;
            var farming = new FarmingSystem(state);
            state.Save.seeds[dayCrop.id] = 1;

            farming.Till(8, 8);
            farming.Plant(8, 8, dayCrop.id);
            farming.Water(8, 8);
            var plot = farming.PlotAt(8, 8);

            _time.timeOfDay = 0.9f;
            AgeCropOneStage(farming, state, 8, 8);
            farming.FastForward();
            Assert.AreEqual(0, plot.crop.stage, $"{dayCrop.id} (growsAt=day) must NOT advance at night");

            _time.timeOfDay = 0.5f;
            farming.FastForward();
            Assert.AreEqual(1, plot.crop.stage, $"{dayCrop.id} advances by day");
            Debug.Log($"[P7Test] {dayCrop.id}: gated at night, grew at noon ✓");
        }

        [Test]
        public void AmbientIntensity_FollowsTimeOfDay()
        {
            _time.timeOfDay = 0.5f;
            float noon = _time.AmbientIntensity();
            _time.timeOfDay = 0.0f;
            float midnight = _time.AmbientIntensity();
            Assert.Greater(noon, 0.95f, "noon near full brightness");
            Assert.Less(midnight, 0.25f, "midnight near floor");
            Debug.Log($"[P7Test] ambient noon={noon:F2} midnight={midnight:F2}");
        }

        [UnityTest]
        public IEnumerator LightingShift_NoonVersusMidnight_Screenshots()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var lighting = DayNightLighting.Create();
            yield return null;

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.transform.position = new Vector3(20f, 20f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            DayNightLighting.Assign2DRenderer(cam);   // Light2D needs the 2D renderer

            Color noonAvg = default, nightAvg = default;
            foreach (var (t, name) in new[] { (0.5f, "noon"), (0.95f, "midnight") })
            {
                _time.timeOfDay = t;
                yield return null;   // DayNightLighting.Update applies
                yield return null;

                var rt = new RenderTexture(600, 600, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(600, 600, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 600, 600), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", $"phase7_{name}.png"), tex.EncodeToPNG());

                var px = tex.GetPixels32();
                float sum = 0f;
                for (int i = 0; i < px.Length; i += 37) sum += (px[i].r + px[i].g + px[i].b) / (3f * 255f);
                var avg = sum / (px.Length / 37f);
                if (name == "noon") noonAvg = new Color(avg, avg, avg); else nightAvg = new Color(avg, avg, avg);
                Debug.Log($"[P7Test] {name} avg brightness={avg:F3} → Logs/phase7_{name}.png");
            }

            Assert.Greater(noonAvg.r, nightAvg.r * 1.5f,
                "noon frame should be clearly brighter than midnight frame");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lighting.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }
    }
}
