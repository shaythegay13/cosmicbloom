// P3-Phase 17 LIVE verification (2026-07-21) — drives the whole farm loop
// through the REAL interaction entry point PlayerController.OnTapWorld (the exact
// method a screen tap calls), against the real runtime wiring (RegionManager +
// FarmGrid registry + FarmPlot colliders + ToolService + tier soil + the real
// HudUI). It never calls FarmingSystem.Till/Plant/... directly — that is what the
// existing suite does and what this pass deliberately avoids. A screenshot and a
// state dump are produced after every step (Logs/verify_stepN_*.png) so a human
// can see exactly what a player would see, and so missing feedback is visible.
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

namespace CosmicBloom.Tests
{
    public class Phase17LiveVerifyTests
    {
        Camera _cam;
        Canvas _canvas;

        void Capture(string label, Vector3 focus)
        {
            _cam.transform.position = new Vector3(focus.x, focus.y, -10f);
            const int w = 720, h = 1280;
            var rt = new RenderTexture(w, h, 24);
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", $"verify_{label}.png"),
                               tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
        }

        [UnityTest]
        public IEnumerator FarmLoop_EndToEnd_ThroughRealTaps()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();

            // ── real runtime wiring (mirrors GameBootstrap, no disk SaveManager) ──
            var regions = new GameObject("Game").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);   // builds tilemaps + ambient decorations
            var state = new GameState(new SaveSnapshot());
            CropSelection.GrantStarterSeeds(state);         // 5 Starlight Berry seeds, as a new game
            state.Save.coins = 40;                          // small purse, so a coin change is obvious
            var farming = new FarmingSystem(state);
            var time = regions.gameObject.AddComponent<TimeManager>();
            time.timeOfDay = 0.5f;                          // midday, grow window open
            var engine = regions.gameObject.AddComponent<QuestEngine>();
            engine.Bind(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);

            var player = PlayerController.Spawn(new Vector3(20, 20));

            // real HUD, rendered over the world so coin/XP feedback is visible in shots
            var hud = HudUI.Create(state, engine, regions);
            _canvas = UIRoot.Canvas;

            _cam = new GameObject("VerifyCam") { tag = "MainCamera" }.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 5f;
            _cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _cam;
            _canvas.planeDistance = 1f;
            yield return null;

            const int X = 4, Y = 4;                          // wild OUTER-RING cell, far from the old core
            var tile = new Vector3(X + 0.5f, Y + 0.5f, 0f);
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(X, Y), "chosen cell is wild tier 0");
            Debug.Log($"[LIVE] target outer-ring cell ({X},{Y}) tier={RegionWorldBuilder.TierAt(X, Y)} " +
                      $"| any collider under it? {(Physics2D.OverlapPoint(tile) != null)}");

            // ═══ STEP 1 — tap bare outer-ring ground from across the region; walk; till ═══
            Debug.Log($"[LIVE] step1: player at {player.transform.position}, tapping bare ground at {tile}");
            player.OnTapWorld(tile);
            Debug.Log($"[LIVE] step1: after tap → MoveTarget={player.MoveTarget}, plot exists yet? {farming.PlotAt(X, Y) != null}");
            Assert.IsNotNull(player.MoveTarget, "tap on distant ground makes the player WALK (not instant-till)");
            Assert.IsNull(farming.PlotAt(X, Y), "nothing tilled until the player arrives");

            for (int i = 0; i < 2000 && player.MoveTarget != null; i++) player.MoveStep(0.05f);
            yield return new WaitForFixedUpdate();           // let the new FarmPlot collider register

            var plot = farming.PlotAt(X, Y);
            Assert.IsNotNull(plot, "arrived and tilled");
            Assert.IsTrue(plot.tilled);
            Debug.Log($"[LIVE] step1 RESULT: tilled ({X},{Y}) soilHealth={plot.soilHealth} " +
                      $"(lush core would seed 100) | view spawned? {GameObject.Find($"Plot_{X}_{Y}") != null}");
            Capture("step1_till", tile);

            // ═══ STEP 2 — tap the tilled plot to plant ═══
            int coinsBeforePlant = state.Save.coins;
            string selCrop = CropSelection.SelectedFor(state, "starlight_vale");
            state.Save.seeds.TryGetValue(selCrop, out int seedsBefore);
            player.OnTapWorld(tile);
            var def = plot.crop != null ? GameDatabase.Crop(plot.crop.id) : null;
            Debug.Log($"[LIVE] step2 RESULT: selectedCrop={selCrop} planted={(plot.crop?.id ?? "NONE")} " +
                      $"seeds {seedsBefore}→{(state.Save.seeds.TryGetValue(selCrop, out var sa) ? sa : 0)} " +
                      $"coins {coinsBeforePlant}→{state.Save.coins} needsWater={plot.crop?.needsWater}");
            Assert.IsNotNull(plot.crop, "tapping the tilled plot planted the region's selected crop");
            yield return null;
            Capture("step2_plant", tile);

            // ═══ STEP 3 — tap to water ═══
            bool neededWater = plot.crop.needsWater;
            player.OnTapWorld(tile);
            Debug.Log($"[LIVE] step3 RESULT: needsWater {neededWater}→{plot.crop.needsWater}");
            Assert.IsFalse(plot.crop.needsWater, "tapping again watered the crop");
            yield return null;
            Capture("step3_water", tile);

            // ═══ STEP 4 — advance grow stages (fast-forward the stage timer) ═══
            int startStage = plot.crop.stage;
            for (int guard = 0; plot.crop != null && plot.crop.stage < def.stages - 1 && guard < 20; guard++)
            {
                plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - def.GrowMsPerStage - 1;
                farming.FastForward();
                if (plot.crop != null && plot.crop.needsWater) player.OnTapWorld(tile);   // re-water via tap
            }
            yield return null;   // FarmPlot.LateUpdate refreshes the visual from live state
            Debug.Log($"[LIVE] step4 RESULT: stage {startStage}→{plot.crop.stage} of {def.stages - 1} " +
                      $"(action now = {farming.ActionFor(X, Y)})");
            Assert.Greater(plot.crop.stage, startStage, "crop visibly advanced through grow stages");
            Assert.AreEqual(def.stages - 1, plot.crop.stage, "grew to the harvest-ready stage");
            Capture("step4_grown", tile);

            // ═══ STEP 5 — tap to harvest; confirm coin + XP gain ═══
            int coinsBefore = state.Save.coins, xpBefore = state.Save.xp;
            player.OnTapWorld(tile);
            Debug.Log($"[LIVE] step5 RESULT: crop after harvest={(plot.crop?.id ?? "NONE (harvested)")} " +
                      $"coins {coinsBefore}→{state.Save.coins} (+{state.Save.coins - coinsBefore}, sellPrice={def.sellPrice}) " +
                      $"xp {xpBefore}→{state.Save.xp} (+{state.Save.xp - xpBefore}, cropXp={def.xp}) " +
                      $"restoration={state.CurrentFarm.restoration:F1}");
            Assert.IsNull(plot.crop, "harvest emptied the plot");
            Assert.AreEqual(coinsBefore + def.sellPrice, state.Save.coins, "coins increased by sell price");
            Assert.AreEqual(xpBefore + def.xp, state.Save.xp, "xp increased by crop xp");
            yield return null;
            Capture("step5_harvest", tile);

            Debug.Log("[LIVE] full tap-driven loop complete — see Logs/verify_step1..5_*.png");

            // teardown
            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(hud.QuestLog.gameObject);
            Object.DestroyImmediate(hud.Map.gameObject);
            Object.DestroyImmediate(hud.Pause.gameObject);
            Object.DestroyImmediate(hud.Ranch.gameObject);
            Object.DestroyImmediate(hud.gameObject);
            Object.DestroyImmediate(_cam.gameObject);
            foreach (var d in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(d.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
        }
    }
}
