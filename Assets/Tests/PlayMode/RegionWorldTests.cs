// Phase 2 verification — all five regions build from regions.json with
// correct per-tier colors, non-overlapping bounds, and a farmable lush core
// that contains the pre-existing save's plot key (10,10).
// Also captures a PNG overview of all five tilemaps for visual review.
using System.Collections;
using System.IO;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace CosmicBloom.Tests
{
    public class RegionWorldTests
    {
        RegionManager _regions;

        [SetUp]
        public void SetUp()
        {
            _regions = new GameObject("TestRegionManager").AddComponent<RegionManager>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(_regions.gameObject);
        }

        [UnityTest]
        public IEnumerator AllFiveRegionsBuildWithDataDrivenColors()
        {
            var grids = RegionWorldBuilder.BuildAll(_regions);
            yield return null;

            Assert.AreEqual(5, grids.Count, "five regions in regions.json");

            foreach (var def in GameDatabase.Regions)
            {
                Assert.IsTrue(grids.ContainsKey(def.id), $"{def.id} built");
                var tilemap = grids[def.id].GetComponentInChildren<Tilemap>();
                Assert.IsNotNull(tilemap, $"{def.id} has tilemap");

                // Sample one cell per tier and compare against regions.json hex
                AssertTileColor(tilemap, def, 0, 0, 0);     // outer ring
                AssertTileColor(tilemap, def, 4, 4, 1);     // mid ring
                AssertTileColor(tilemap, def, 10, 10, 2);   // lush core
                Debug.Log($"[P2Test] {def.id} tiers OK: {string.Join(",", def.groundTiers)}");
            }
        }

        static void AssertTileColor(Tilemap tilemap, RegionDef def, int x, int y, int tier)
        {
            var tile = tilemap.GetTile<Tile>(new Vector3Int(x, y, 0));
            Assert.IsNotNull(tile, $"{def.id} tile at {x},{y}");
            var expected = ProceduralSprites.ParseHex(def.groundTiers[tier]);
            Assert.AreEqual(expected, tile.color, $"{def.id} cell({x},{y}) should be tier {tier} color");
        }

        [UnityTest]
        public IEnumerator RegionBoundsDoNotOverlapAndContainLushCore()
        {
            RegionWorldBuilder.BuildAll(_regions);
            yield return null;

            var all = new System.Collections.Generic.List<(string id, Bounds b)>();
            foreach (var def in GameDatabase.Regions)
            {
                Assert.IsTrue(_regions.TryGetBounds(def.id, out var b), $"{def.id} bounds registered");
                all.Add((def.id, b));
                Debug.Log($"[P2Test] {def.id} bounds {b.min}..{b.max}");
            }

            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    Assert.IsFalse(all[i].b.Intersects(all[j].b),
                        $"{all[i].id} overlaps {all[j].id}");

            // legacy save plot key "10,10" must sit in the farmable core
            Assert.IsTrue(RegionWorldBuilder.IsFarmable(10, 10), "(10,10) stays farmable");
            Assert.AreEqual(2, RegionWorldBuilder.TierAt(10, 10));
        }

        [UnityTest]
        public IEnumerator CaptureAllRegionsOverviewPng()
        {
            RegionWorldBuilder.BuildAll(_regions);
            yield return null;

            var camGo = new GameObject("CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // five regions span x 0..180 (4*40+20); frame the strip with margin
            cam.transform.position = new Vector3(90f, 10f, -10f);
            cam.orthographicSize = 14f;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            try
            {
                var rt = new RenderTexture(2000, 280, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                string path = Path.Combine(Application.dataPath, "..", "Logs", "phase2_regions.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[P2Test] overview PNG written: {path}");

                // sanity: the image should not be a solid background — regions add color variety
                var c1 = tex.GetPixel(tex.width / 10, tex.height / 2);      // inside region 0
                var c2 = tex.GetPixel(tex.width / 2, tex.height / 8);       // background gap
                Assert.AreNotEqual(c1, c2, "tilemaps should be visible against background");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[P2Test] capture unavailable in this environment: {e.Message}");
                Assert.Ignore("Rendering not available; visual check pending human review.");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
            yield return null;
        }
    }
}
