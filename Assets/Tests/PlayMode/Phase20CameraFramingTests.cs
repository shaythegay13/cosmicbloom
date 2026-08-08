// P3-Phase 20 verification — camera framing retune for the 40x40 regions.
//
// Background: `orthographicSize` was set to 7f in Phase 2, when regions were
// 20x20. P3-Phase 15 quadrupled region area (40x40) and edited only the trailing
// comment, never the value — so the live view showed ~7% of a region and none of
// Starlight Vale's 3 landmarks were on screen from the spawn point.
//
// These tests cover the four things that retune has to get right:
//   1. SpawnFraming_CandidateSweep_Screenshots — the before/after evidence, and a
//      re-runnable sweep so a future re-tune is a screenshot comparison, not a guess.
//   2. AllValeLandmarks_ReachableAndOnScreen_ViaRealMovement — every landmark is
//      walkable via real taps AND inside the camera rect when you arrive.
//   3. CameraNeverShowsOutsideRegion_AtAnyPannedCorner — the clamp still holds at
//      the larger size (a bigger halfH/halfW shrinks the legal camera box).
//   4. SpriteFootprint_StaysAboveLegibilityFloor — the visibility/readability
//      trade-off, measured in screen pixels rather than asserted by eye.
using System.Collections;
using System.Collections.Generic;
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
    public class Phase20CameraFramingTests
    {
        // Portrait phone. ProjectSettings has defaultScreenOrientation: 0 (Portrait);
        // 720x1280 is the same capture size CoreUiTests/CropSelectorTests already use.
        const int W = 720, H = 1280;
        const float Aspect = (float)W / H;

        // Source art is 64x64 px at spritePixelsToUnits 64 (see any
        // Assets/Resources/Sprites/Generated/**/*.png.meta), so one character sprite
        // is exactly 1 world unit tall. That makes screen-px-per-unit the whole
        // legibility story and lets the floor below be a real number.
        const float SpriteSourcePx = 64f;

        RegionManager _regions;
        Dictionary<string, Grid> _grids;
        GameState _state;
        FarmingSystem _farming;
        List<LandmarkController> _landmarks;
        PlayerController _player;

        static float PxPerUnit(float orthoSize) => H / (2f * orthoSize);

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _state = new GameState(new SaveSnapshot());
            _state.Save.currentRegion = "starlight_vale";
            _regions.SetCurrentRegion("starlight_vale");
            _farming = new FarmingSystem(_state);
            foreach (var kv in _grids) FarmGrid.Create(kv.Key, kv.Value, _farming, _state);
            _landmarks = LandmarkSpawner.SpawnAll(_regions, _state);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.DestroyImmediate(_player.gameObject);
            foreach (var lm in _landmarks.Where(l => l != null))
                Object.DestroyImmediate(lm.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        /// <summary>Spawn the player where GameBootstrap does — region centre.</summary>
        PlayerController SpawnPlayerAtRegionCentre(string regionId)
        {
            _regions.TryGetBounds(regionId, out var b);
            _player = PlayerController.Spawn(b.center);
            return _player;
        }

        static (Camera cam, CameraFollow follow) MakeFollowCamera(Transform target, float size)
        {
            var camGo = new GameObject("FramingCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.aspect = Aspect;   // batchmode: don't inherit the RT/desktop aspect
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            var follow = camGo.AddComponent<CameraFollow>();
            follow.target = target;
            follow.SnapTo(target.position);
            return (cam, follow);
        }

        /// <summary>The camera's world-space view rect, honouring the explicit test aspect.</summary>
        static Rect ViewRect(Camera cam)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            var p = cam.transform.position;
            return new Rect(p.x - halfW, p.y - halfH, halfW * 2f, halfH * 2f);
        }

        static bool RectContainsPoint(Rect r, Vector3 p) =>
            p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;

        /// <summary>Put the camera at its settled position, deterministically.
        /// A fixed frame count is NOT enough here: headless batchmode frames run at a
        /// sub-millisecond deltaTime, so after 240 frames SmoothDamp had covered only
        /// ~48% of the distance and the assertions read a still-moving camera (two false
        /// failures on the first run of this file). SnapTo routes through the same
        /// ClampedFocus as LateUpdate, so this still exercises the real clamp path.</summary>
        static IEnumerator SettleCamera(CameraFollow follow, Transform target)
        {
            follow.SnapTo(target.position);
            yield return null;   // one LateUpdate must hold it there, not drift off
        }

        /// <summary>Give a region some foreground detail so the screenshots show what a
        /// player actually looks at, not bare tilemap: mature crops around the spawn point.</summary>
        void PlantAroundCentre(string regionId)
        {
            _regions.TryGetBounds(regionId, out var b);
            var origin = b.center - new Vector3(RegionWorldBuilder.Size / 2f, RegionWorldBuilder.Size / 2f);
            var crop = GameDatabase.Crops.FirstOrDefault(c => c.region == regionId);
            if (crop == null) return;
            int cx = Mathf.RoundToInt(b.center.x - origin.x), cy = Mathf.RoundToInt(b.center.y - origin.y);
            for (int dx = -4; dx <= 4; dx += 2)
                for (int dy = -4; dy <= 4; dy += 2)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!_farming.Till(x, y)) continue;
                    _state.Save.seeds[crop.id] = 1;
                    if (!_farming.Plant(x, y, crop.id)) continue;
                    var p = _farming.PlotAt(x, y);
                    p.crop.stage = crop.stages - 1;
                    p.crop.needsWater = false;
                }
        }

        static void Capture(Camera cam, string fileName)
        {
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", fileName), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ── 1. before/after + candidate sweep ────────────────────────────────────
        [UnityTest]
        public IEnumerator SpawnFraming_CandidateSweep_Screenshots()
        {
            // 7f is the pre-Phase-20 value (the "before"); 13f is DefaultOrthographicSize.
            float[] candidates = { 7f, 12f, 13f, 14f };
            string[] regionIds = { "starlight_vale", "sun_garden", "nebula_garden" };

            foreach (var regionId in regionIds)
            {
                _regions.SetCurrentRegion(regionId);
                PlantAroundCentre(regionId);
                NpcSpawner.SpawnAll(_regions, _state);
                var player = SpawnPlayerAtRegionCentre(regionId);
                yield return null;   // let FarmPlot/NPC visuals refresh from live state
                yield return null;

                var (cam, _) = MakeFollowCamera(player.transform, candidates[0]);
                foreach (var size in candidates)
                {
                    cam.orthographicSize = size;
                    cam.aspect = Aspect;
                    yield return null;
                    Capture(cam, $"phase20_framing_{regionId}_ortho{size:00}.png");
                    Debug.Log($"[P20Framing] {regionId} @ ortho {size}: view {size * Aspect * 2f:F1}x{size * 2f:F1} units " +
                              $"({size * Aspect * 2f / RegionWorldBuilder.Size * 100f:F0}% x {size * 2f / RegionWorldBuilder.Size * 100f:F0}% of the region), " +
                              $"{PxPerUnit(size):F1} screen px per world unit");
                }
                Object.DestroyImmediate(cam.gameObject);
                foreach (var npc in Object.FindObjectsByType<NpcController>())
                    Object.DestroyImmediate(npc.gameObject);
                Object.DestroyImmediate(player.gameObject);
                _player = null;
            }
        }

        // ── 2. every Vale landmark reachable AND on screen ───────────────────────
        [UnityTest]
        public IEnumerator AllValeLandmarks_ReachableAndOnScreen_ViaRealMovement()
        {
            var player = SpawnPlayerAtRegionCentre("starlight_vale");
            var (cam, follow) = MakeFollowCamera(player.transform, CameraFollow.DefaultOrthographicSize);
            _regions.TryGetBounds("starlight_vale", out var bounds);

            var vale = _landmarks.Where(l => l.RegionId == "starlight_vale").ToList();
            Assert.AreEqual(3, vale.Count, "Starlight Vale has 3 landmarks in regions.json");

            // Sanity: the bug being fixed — at the old 7f none of them were on screen at spawn.
            cam.orthographicSize = 7f;
            yield return null;
            var spawnRectOld = ViewRect(cam);
            int visibleOld = vale.Count(l => RectContainsPoint(spawnRectOld, l.transform.position));
            cam.orthographicSize = CameraFollow.DefaultOrthographicSize;

            foreach (var lm in vale)
            {
                player.transform.position = bounds.center;   // always start the walk from spawn
                player.OnTapWorld(lm.InteractPosition);
                for (int i = 0; i < 4000 && player.MoveTarget != null; i++) player.MoveStep(0.05f);

                float dist = Vector2.Distance(player.transform.position, lm.InteractPosition);
                Assert.LessOrEqual(dist, player.interactRange + 0.01f,
                    $"{lm.Def.id}: player walked into interact range via a real tap (dist {dist:F2})");

                yield return SettleCamera(follow, player.transform);
                var rect = ViewRect(cam);

                Assert.IsTrue(RectContainsPoint(rect, lm.transform.position),
                    $"{lm.Def.id} at {lm.transform.position} is inside the camera view {rect}");
                Assert.IsTrue(RectContainsPoint(rect, player.transform.position),
                    $"player is still on screen while standing at {lm.Def.id}");
                Assert.IsTrue(bounds.Contains(new Vector3(rect.xMin, rect.yMin, bounds.center.z)) &&
                              bounds.Contains(new Vector3(rect.xMax, rect.yMax, bounds.center.z)),
                    $"camera view {rect} stays inside the region while framing {lm.Def.id}");

                Debug.Log($"[P20Framing] {lm.Def.id} ({lm.Def.restoreCost}c) at {lm.transform.position} — " +
                          $"reached (dist {dist:F2}), on screen in view {rect}");
            }

            // What the retune does and does not fix, pinned so nobody over-claims it.
            // From the spawn point (region centre) the landmark slots sit at (0,+14) and
            // (±13,+11). Portrait aspect makes X the binding constraint: halfW is only
            // 0.5625*size, so seeing x=±13 would need ortho >= 23 — past the region's own
            // half-height, where CameraFollow pins to centre and stops following at all.
            // So zoom alone surfaces the near landmark, not the two side ones; closing
            // that gap is a signposting job (map markers / cost display), not a camera one.
            var spawnRectNew = ViewRectAt(cam, bounds.center, CameraFollow.DefaultOrthographicSize);
            int visibleNew = vale.Count(l => RectContainsPoint(spawnRectNew, l.transform.position));

            Assert.AreEqual(0, visibleOld,
                "regression pin: at the old ortho 7 NO Vale landmark was on screen from the spawn point");
            Assert.Greater(visibleNew, visibleOld,
                "the retune puts at least one more landmark on screen from the spawn point");
            Debug.Log($"[P20Framing] landmarks on screen from spawn: {visibleOld}/3 at the old ortho 7 -> " +
                      $"{visibleNew}/3 at ortho {CameraFollow.DefaultOrthographicSize}. The remaining " +
                      $"{3 - visibleNew} sit at x=±13, unreachable in portrait without pinning the camera — " +
                      $"a signposting gap, not a framing one.");
            Object.DestroyImmediate(cam.gameObject);
        }

        static Rect ViewRectAt(Camera cam, Vector3 centre, float size)
        {
            float halfW = size * cam.aspect;
            return new Rect(centre.x - halfW, centre.y - size, halfW * 2f, size * 2f);
        }

        // ── 3. clamp still holds at the larger size ──────────────────────────────
        [UnityTest]
        public IEnumerator CameraNeverShowsOutsideRegion_AtAnyPannedCorner()
        {
            var player = SpawnPlayerAtRegionCentre("starlight_vale");
            var (cam, follow) = MakeFollowCamera(player.transform, CameraFollow.DefaultOrthographicSize);
            _regions.TryGetBounds("starlight_vale", out var bounds);

            // Drive the player hard into all four corners (and past them — the player is
            // clamped by RegionManager, the camera by CameraFollow; both must hold).
            var targets = new[]
            {
                bounds.min, bounds.max,
                new Vector3(bounds.min.x, bounds.max.y), new Vector3(bounds.max.x, bounds.min.y),
                bounds.center + new Vector3(0f, 100f), bounds.center + new Vector3(-100f, 0f),
            };

            foreach (var t in targets)
            {
                player.SetMoveTarget(t);
                for (int i = 0; i < 6000 && player.MoveTarget != null; i++) player.MoveStep(0.05f);
                yield return SettleCamera(follow, player.transform);

                var rect = ViewRect(cam);
                Assert.GreaterOrEqual(rect.xMin, bounds.min.x - 0.01f, $"view left edge inside region (target {t})");
                Assert.LessOrEqual(rect.xMax, bounds.max.x + 0.01f, $"view right edge inside region (target {t})");
                Assert.GreaterOrEqual(rect.yMin, bounds.min.y - 0.01f, $"view bottom edge inside region (target {t})");
                Assert.LessOrEqual(rect.yMax, bounds.max.y + 0.01f, $"view top edge inside region (target {t})");
                Assert.IsTrue(RectContainsPoint(rect, player.transform.position),
                    $"player stays on screen at the clamped corner (target {t})");
            }

            // The clamp box must not have collapsed: at ortho S the camera may pan within
            // [min+halfW, max-halfW] x [min+halfH, max-halfH]. If 2*halfH >= region size the
            // camera pins to centre and stops following at all — that is the failure mode a
            // too-large size introduces, so assert we are clear of it.
            float halfH = CameraFollow.DefaultOrthographicSize;
            float halfW = halfH * Aspect;
            Assert.Less(halfW * 2f, RegionWorldBuilder.Size,
                "horizontal pan range is non-zero (camera is not pinned to region centre on X)");
            Assert.Less(halfH * 2f, RegionWorldBuilder.Size,
                "vertical pan range is non-zero (camera is not pinned to region centre on Y)");
            Debug.Log($"[P20Framing] ortho {halfH}: pan box {RegionWorldBuilder.Size - halfW * 2f:F1} x " +
                      $"{RegionWorldBuilder.Size - halfH * 2f:F1} units inside the 40x40 region");
            Object.DestroyImmediate(cam.gameObject);
        }

        // ── 4. the visibility / readability trade-off, measured ──────────────────
        [Test]
        public void SpriteFootprint_StaysAboveLegibilityFloor()
        {
            float size = CameraFollow.DefaultOrthographicSize;
            float pxPerUnit = PxPerUnit(size);

            // A character/crop sprite is 1 world unit tall (64px @ PPU 64), so it renders
            // at exactly pxPerUnit screen pixels on a 1280px-tall portrait frame.
            // Floor: 44px — Apple HIG's minimum interactive target, and the practical
            // lower bound for a readable pixel-art character on a phone.
            Assert.GreaterOrEqual(pxPerUnit, 44f,
                $"a 1-unit sprite renders at {pxPerUnit:F1}px tall at ortho {size} — below the 44px readability floor");

            // Point-filtered art below 1:1 drops source rows unevenly. Log the scale factor
            // so the trade-off is on the record rather than discovered later as shimmer.
            float scaleVsSource = pxPerUnit / SpriteSourcePx;
            Debug.Log($"[P20Framing] ortho {size}: {pxPerUnit:F1} screen px per world unit; " +
                      $"64px source art renders at {scaleVsSource:F2}x " +
                      $"({(scaleVsSource >= 1f ? "upscaled, no row loss" : "downscaled — Point filtering drops source rows")})");

            // Guard the other direction too: the old 7f is what regressed into 40x40 regions
            // unnoticed. Assert the shipped value actually shows a useful slice of a region.
            float visibleFraction = (size * Aspect * 2f) * (size * 2f)
                                    / (RegionWorldBuilder.Size * RegionWorldBuilder.Size);
            Assert.Greater(visibleFraction, 0.15f,
                $"ortho {size} shows {visibleFraction * 100f:F1}% of a 40x40 region — too little to navigate by");
            Debug.Log($"[P20Framing] ortho {size} shows {visibleFraction * 100f:F1}% of a region " +
                      $"(the pre-Phase-20 7f showed {(7f * Aspect * 2f) * 14f / 1600f * 100f:F1}%)");
        }
    }
}
