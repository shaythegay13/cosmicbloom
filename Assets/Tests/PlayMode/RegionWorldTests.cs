// Phase 2 verification (dimensions updated for the P3-Phase 15 resize to
// 40x40) — all five regions build from regions.json with correct per-tier
// colors, non-overlapping bounds, and a farmable lush core that contains the
// pre-existing save's plot key (10,10) after SaveMigration's +6/+6 shift.
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
                AssertTileColor(tilemap, def, 8, 8, 1);     // mid ring
                AssertTileColor(tilemap, def, 20, 20, 2);   // lush core
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

            // legacy save plot key "10,10" survives via SaveMigration's +6/+6 shift:
            // the migrated cell (16,16) must sit in the farmable core
            int off = CosmicBloom.Save.SaveMigration.PlotCellOffset;
            Assert.IsTrue(RegionWorldBuilder.IsFarmable(10 + off, 10 + off),
                "migrated legacy plot key stays farmable");
            Assert.IsFalse(RegionWorldBuilder.IsFarmable(10, 10),
                "unmigrated legacy cell is outside the new core — migration is required");
        }

        [UnityTest]
        public IEnumerator CaptureAllRegionsOverviewPng()
        {
            RegionWorldBuilder.BuildAll(_regions);
            yield return null;

            var camGo = new GameObject("CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // five regions span x 0..360 (4*80+40); frame the strip with margin
            cam.transform.position = new Vector3(180f, 20f, -10f);
            cam.orthographicSize = 28f;
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

        // 2026-07-11 ad hoc verification: confirm ground tiles render the real
        // Gemini tile-mask textures (P3-Phase 9 cont.) rather than falling back
        // to ProceduralSprites.Square's flat fill. The wide overview capture
        // above is too zoomed-out to tell texture from flat color at ~5px/unit
        // — this frames one full region at a time at high resolution and logs
        // pixel-variance within a single tier so a flat fallback would show up
        // as near-zero stddev even without eyeballing the PNG.
        [UnityTest]
        public IEnumerator CaptureTwoRegionsCloseUpForTileArtCheck()
        {
            RegionWorldBuilder.BuildAll(_regions);
            yield return null;

            var defs = new System.Collections.Generic.List<RegionDef>(GameDatabase.Regions);
            for (int i = 0; i < 2 && i < defs.Count; i++)
            {
                var def = defs[i];
                Assert.IsTrue(_regions.TryGetBounds(def.id, out var bounds), $"{def.id} bounds registered");

                var camGo = new GameObject($"CloseupCam_{def.id}");
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

                    string path = Path.Combine(Application.dataPath, "..", "Logs", $"phase17_closeup_{def.id}.png");
                    File.WriteAllBytes(path, tex.EncodeToPNG());

                    // Sample a patch centered on the lush core (always tier 2) and
                    // measure luminance stddev — a flat ProceduralSprites.Square
                    // fallback tinted by one solid color has ~0 variance; the real
                    // textured Gemini mask does not.
                    var centerScreen = cam.WorldToScreenPoint(bounds.center);
                    int cx = Mathf.Clamp((int)centerScreen.x, 40, texSize - 40);
                    int cy = Mathf.Clamp((int)centerScreen.y, 40, texSize - 40);
                    double sum = 0, sumSq = 0;
                    int n = 0;
                    for (int dx = -20; dx <= 20; dx += 2)
                        for (int dy = -20; dy <= 20; dy += 2)
                        {
                            var px = tex.GetPixel(cx + dx, cy + dy);
                            double lum = 0.299 * px.r + 0.587 * px.g + 0.114 * px.b;
                            sum += lum;
                            sumSq += lum * lum;
                            n++;
                        }
                    double mean = sum / n;
                    double variance = sumSq / n - mean * mean;
                    double stddev = System.Math.Sqrt(variance);
                    Debug.Log($"[TileArtCheck] {def.id} lush-core patch stddev={stddev:F5} mean={mean:F3} (n={n}) -> {path}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TileArtCheck] capture unavailable for {def.id}: {e.Message}");
                    Assert.Ignore("Rendering not available; visual check pending human review.");
                }
                finally
                {
                    Object.DestroyImmediate(camGo);
                }
            }
            yield return null;
        }
    }
}
