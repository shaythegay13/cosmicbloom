// P3-Phase 17 verification — free-form farm plot placement replaces the fixed
// 256-cell lush-core grid. This render test proves it visually: it tills plots
// in a free-form scatter that is impossible under the old model — wild-tier
// cells in the outer ring, a diagonal crossing all three tiers, and lone mid-
// ring cells — then captures a top-down PNG of the whole region so the plots'
// positions can be eyeballed against the tier rings (Logs/phase17_freeplace.png).
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
    public class Phase17FreePlacementTests
    {
        [UnityTest]
        public IEnumerator FreePlacement_TillsAcrossAllTiers_Screenshot()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);

            // A free-form scatter that the old fixed grid (cells 12..27 only) could
            // never produce: a wild-ring cluster, a diagonal crossing wild→mid→lush,
            // and lone mid-ring plots. Every one of these is now a legal plot.
            var cells = new System.Collections.Generic.List<Vector2Int>
            {
                new(3, 3), new(4, 3), new(3, 4),         // wild outer-ring cluster (tier 0)
                new(2, 20), new(20, 2),                   // lone wild plots against opposite edges
                new(9, 9), new(30, 30), new(31, 9),       // mid-ring singletons (tier 1)
            };
            for (int i = 6; i <= 30; i += 3) cells.Add(new Vector2Int(i, i));   // diagonal across every tier

            int tilled = 0;
            foreach (var c in cells)
                if (farming.Till(c.x, c.y)) tilled++;
            Assert.Greater(tilled, 10, "a free-form scatter of plots was tilled outside the old grid block");

            // plant a couple of vale-native crops at wild-tier cells so real crop art
            // shows up well away from the lush core, then push them to a mature stage
            var crop = GameDatabase.Crops.First(c => c.region == "starlight_vale");
            foreach (var c in new[] { new Vector2Int(3, 3), new Vector2Int(4, 3) })
            {
                state.Save.seeds[crop.id] = 1;
                if (farming.Plant(c.x, c.y, crop.id))
                {
                    var p = farming.PlotAt(c.x, c.y);
                    p.crop.stage = crop.stages - 1;
                    p.crop.needsWater = false;
                }
            }
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(3, 3), "the planted (3,3) cell is wild tier — nowhere near the old core");

            yield return null;   // FarmPlot.LateUpdate refreshes visuals from live state
            yield return null;

            // top-down capture of the whole 40x40 region
            regions.TryGetBounds("starlight_vale", out var bounds);
            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);
            cam.orthographicSize = bounds.extents.y + 1f;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            try
            {
                const int texSize = 1024;
                var rt = new RenderTexture(texSize, texSize, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, texSize, texSize), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                string path = Path.Combine(Application.dataPath, "..", "Logs", "phase17_freeplace.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[P17Test] free-placement overview PNG written: {path} ({tilled} plots tilled free-form)");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[P17Test] capture unavailable: {e.Message}");
                Assert.Ignore("Rendering not available; visual check pending human review.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
                Object.DestroyImmediate(regions.gameObject);
                GameEvents.Clear();
            }
        }
    }
}
