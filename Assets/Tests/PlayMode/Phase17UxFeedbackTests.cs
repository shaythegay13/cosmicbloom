// P3-Phase 17 UX feedback fixes — verifies the five findings from the live
// end-to-end farm-loop verification (2026-07-23):
//  A. wild/mid/lush tilled plots visibly differ (soil quality was invisible)
//  B. a growing crop sorts above the player instead of being fully hidden
//  C. planting now spawns an FX burst (was the only action with none)
//  D. a "Tap to till/plant/water/harvest" label follows the player
//  E. harvest spawns a floating "+coins/+xp" popup at the plot, not just the HUD
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
    public class Phase17UxFeedbackTests
    {
        /// <summary>FarmActionEffects particles and RewardPopups are deliberately
        /// unparented fire-and-forget GameObjects that self-destroy after their own
        /// short lifetime — nothing in a region/grid teardown cleans them up. Any test
        /// that triggers a real till/plant/water/harvest must sweep them here so it
        /// doesn't leak a still-fading burst/popup into whichever test runs next.</summary>
        static void ClearFireAndForgetVfx()
        {
            foreach (var fx in Object.FindObjectsByType<FarmActionEffectParticle>(FindObjectsSortMode.None))
                Object.DestroyImmediate(fx.gameObject);
            foreach (var p in Object.FindObjectsByType<RewardPopup>(FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
        }

        [Test]
        public void SoilTint_DiffersAcrossTiers()
        {
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);

            Assert.AreEqual(2, RegionWorldBuilder.TierAt(20, 20));
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(28, 20));
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(3, 3));

            farming.Till(20, 20);
            farming.Till(28, 20);
            farming.Till(3, 3);

            var lush = GameObject.Find("Plot_20_20").GetComponent<FarmPlot>();
            var mid = GameObject.Find("Plot_28_20").GetComponent<FarmPlot>();
            var wild = GameObject.Find("Plot_3_3").GetComponent<FarmPlot>();

            Assert.AreNotEqual(lush.CurrentColor, mid.CurrentColor, "lush vs mid tilled tint differ");
            Assert.AreNotEqual(mid.CurrentColor, wild.CurrentColor, "mid vs wild tilled tint differ");
            Assert.AreNotEqual(lush.CurrentColor, wild.CurrentColor, "lush vs wild tilled tint differ");
            Debug.Log($"[P17UX] soil tint by tier: lush={lush.CurrentColor} mid={mid.CurrentColor} wild={wild.CurrentColor}");

            ClearFireAndForgetVfx();   // this test's till-only bursts
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        [Test]
        public void CropPresence_TogglesSortingOrderAboveAndBelowPlayer()
        {
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            state.Save.seeds["starlight_berry"] = 1;
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);
            var player = PlayerController.Spawn(new Vector3(20, 20));
            int playerOrder = player.GetComponentInChildren<SpriteRenderer>().sortingOrder;

            farming.Till(20, 20);
            var plot = GameObject.Find("Plot_20_20").GetComponent<FarmPlot>();
            Assert.Less(plot.CurrentSortingOrder, playerOrder, "empty tilled dirt sorts below the player");

            Assert.IsTrue(farming.Plant(20, 20, "starlight_berry"));
            Assert.Greater(plot.CurrentSortingOrder, playerOrder,
                "a growing crop sorts ABOVE the player — not fully hidden while standing on the tile");

            var def = GameDatabase.Crop("starlight_berry");
            farming.PlotAt(20, 20).crop.stage = def.stages - 1;
            farming.PlotAt(20, 20).crop.needsWater = false;
            Assert.IsTrue(farming.Harvest(20, 20));
            Assert.Less(plot.CurrentSortingOrder, playerOrder, "back below the player once harvested (bare dirt again)");

            ClearFireAndForgetVfx();   // till/plant/harvest bursts + the harvest's reward popup
            Object.DestroyImmediate(player.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        [UnityTest]
        public IEnumerator Planting_SpawnsFxBurst()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            state.Save.seeds["starlight_berry"] = 1;
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);
            farming.Till(20, 20);
            yield return null;

            // Delta-based, not absolute-zero: FarmActionEffects particles are deliberately
            // unparented fire-and-forget GameObjects (self-destroy after their own short
            // lifetime) so they aren't cleaned up by this test's region teardown — an
            // earlier test's still-fading burst can legitimately still exist here.
            int PlantFxCount() => Object.FindObjectsByType<FarmActionEffectParticle>(FindObjectsSortMode.None)
                .Count(p => p.gameObject.name == "FX_plant");

            int before = PlantFxCount();
            Assert.IsTrue(farming.Plant(20, 20, "starlight_berry"));
            Assert.AreEqual(before + 6, PlantFxCount(),
                "planting now spawns a leafy FX burst (was the only action with none)");

            ClearFireAndForgetVfx();
            GameEvents.Clear();
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        [UnityTest]
        public IEnumerator ContextPrompt_ShowsCorrectVerbThroughFullLoop()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            state.Save.seeds["starlight_berry"] = 1;
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);
            var player = PlayerController.Spawn(new Vector3(4.5f, 4.5f, 0f));   // wild-tier bare ground
            var prompt = FarmActionPrompt.Create(player.transform);
            yield return null;

            Assert.AreEqual("Tap to till", prompt.CurrentLabel, "bare tillable ground");

            farming.Till(4, 4);
            yield return null;
            Assert.AreEqual("Tap to plant", prompt.CurrentLabel, "tilled, empty, soil above the plant floor");

            farming.Plant(4, 4, "starlight_berry");
            yield return null;
            Assert.AreEqual("Tap to water", prompt.CurrentLabel, "freshly planted crop needs its first watering");

            farming.Water(4, 4);
            yield return null;
            Assert.IsNull(prompt.CurrentLabel, "growing and watered — nothing to do right now, prompt hides");

            var def = GameDatabase.Crop("starlight_berry");
            var plot = farming.PlotAt(4, 4);
            for (int guard = 0; plot.crop.stage < def.stages - 1 && guard < 10; guard++)
            {
                plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - def.GrowMsPerStage - 1;
                farming.FastForward();
                if (plot.crop.needsWater) farming.Water(4, 4);
            }
            yield return null;
            Assert.AreEqual("Tap to harvest", prompt.CurrentLabel, "grown to the final stage");

            farming.Harvest(4, 4);
            yield return null;
            Assert.AreEqual("Tap to plant", prompt.CurrentLabel, "harvested — tilled dirt again, soil still plantable");

            ClearFireAndForgetVfx();
            Object.DestroyImmediate(prompt.gameObject);
            Object.DestroyImmediate(player.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator Harvest_SpawnsRewardPopup_WithCoinsAndXp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            state.Save.seeds["starlight_berry"] = 1;
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);

            farming.Till(20, 20);
            farming.Plant(20, 20, "starlight_berry");
            var def = GameDatabase.Crop("starlight_berry");
            var plot = farming.PlotAt(20, 20);
            plot.crop.stage = def.stages - 1;
            plot.crop.needsWater = false;
            yield return null;

            // Delta-based (see Planting_SpawnsFxBurst comment): a still-fading popup from
            // an earlier test can legitimately exist when this test starts.
            var before = Object.FindObjectsByType<RewardPopup>(FindObjectsSortMode.None).ToHashSet();
            Assert.IsTrue(farming.Harvest(20, 20));

            var newPopups = Object.FindObjectsByType<RewardPopup>(FindObjectsSortMode.None)
                .Where(p => !before.Contains(p)).ToList();
            Assert.AreEqual(1, newPopups.Count, "harvest spawned exactly one NEW reward popup");
            string text = newPopups[0].GetComponent<TextMesh>().text;
            Assert.IsTrue(text.Contains(def.sellPrice.ToString()), $"popup shows the coin reward: '{text}'");
            Assert.IsTrue(text.Contains(def.xp.ToString()), $"popup shows the xp reward: '{text}'");
            Debug.Log($"[P17UX] reward popup text: '{text}'");

            ClearFireAndForgetVfx();   // this test's till/plant/harvest bursts + the popup itself
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator UxFeedback_VisualScreenshots()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            ClearFireAndForgetVfx();   // guarantee a clean slate before capturing evidence
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            state.Save.seeds["starlight_berry"] = 2;
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);
            var player = PlayerController.Spawn(new Vector3(20, 20));

            var camGo = new GameObject("UxCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            void Capture(string label, Vector3 focus, float size)
            {
                cam.transform.position = new Vector3(focus.x, focus.y, -10f);
                cam.orthographicSize = size;
                const int w = 900, h = 900;
                var rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", $"ux_{label}.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(rt);
            }

            // ── A: soil tiers side by side — whole-region overview ──
            farming.Till(20, 20);   // lush
            farming.Till(28, 20);   // mid
            farming.Till(3, 3);     // wild
            yield return null;
            regions.TryGetBounds("starlight_vale", out var bounds);
            Capture("A_soil_tiers", bounds.center, bounds.extents.y + 1f);

            // ── B: player standing on a small growing crop — must not fully hide it ──
            player.transform.position = new Vector3(20.5f, 20.5f, 0f);
            Assert.IsTrue(farming.Plant(20, 20, "starlight_berry"));
            yield return null;
            Capture("B_sorting", player.transform.position, 2.5f);

            // ── C: moment of planting at a second plot — leafy FX burst on screen.
            // Tighter zoom than B/E: the burst hasn't moved yet at capture instant (no
            // Update() tick since Plant()), so all 6 dots are still clustered right at
            // the origin — a close zoom is what makes that cluster legibly visible.
            farming.Till(10, 10);
            player.transform.position = new Vector3(10.5f, 10.5f, 0f);
            Assert.IsTrue(farming.Plant(10, 10, "starlight_berry"));
            Capture("C_plant_fx", player.transform.position, 1.0f);   // no yield — full-alpha FX frame

            // ── E: moment of harvest at the first plot — reward popup + FX burst ──
            var def = GameDatabase.Crop("starlight_berry");
            var plot = farming.PlotAt(20, 20);
            plot.crop.stage = def.stages - 1;
            plot.crop.needsWater = false;
            player.transform.position = new Vector3(20.5f, 20.5f, 0f);
            yield return null;
            Assert.IsTrue(farming.Harvest(20, 20));
            Capture("E_reward_popup", player.transform.position, 2.5f);   // no yield — full-alpha popup frame

            Debug.Log("[P17UX] wrote Logs/ux_A_soil_tiers.png, ux_B_sorting.png, ux_C_plant_fx.png, ux_E_reward_popup.png");

            ClearFireAndForgetVfx();
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(player.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }
    }
}
