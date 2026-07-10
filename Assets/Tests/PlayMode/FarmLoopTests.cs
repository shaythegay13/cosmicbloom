// Phase 3 verification — the generic farm loop, replacing the old manual
// T/P/W/G/H keyboard test with real assertions:
//  * full till→plant→water→grow→harvest against save-backed PlotState
//  * region-appropriateness (crops.json region field is enforced)
//  * watering gates every stage
//  * FarmGrid views + player tap interaction across two regions
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class FarmLoopTests
    {
        static GameState NewState(string region = "starlight_vale", int coins = 0)
        {
            GameDatabase.LoadAll();
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = region;
            state.Save.coins = coins;
            return state;
        }

        static void ForceGrowStage(GameState state, FarmingSystem farming, int x, int y)
        {
            var plot = farming.PlotAt(x, y);
            var def = GameDatabase.Crop(plot.crop.id);
            plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                                   - def.GrowMsPerStage - 1;
            farming.FastForward();
        }

        [Test]
        public void FullLoop_TillPlantWaterGrowHarvest()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);
            state.Save.seeds["starlight_berry"] = 1;
            var def = GameDatabase.Crop("starlight_berry");

            Assert.IsTrue(farming.Till(7, 7), "till");
            Assert.IsTrue(farming.Plant(7, 7, "starlight_berry"), "plant");
            Assert.IsFalse(state.Save.seeds.ContainsKey("starlight_berry"), "seed consumed");
            Assert.IsTrue(farming.Water(7, 7), "water");

            var plot = farming.PlotAt(7, 7);
            for (int guard = 0; plot.crop.stage < def.stages - 1 && guard < 10; guard++)
            {
                ForceGrowStage(state, farming, 7, 7);
                if (plot.crop.needsWater) Assert.IsTrue(farming.Water(7, 7), "re-water each stage");
            }
            Assert.AreEqual(def.stages - 1, plot.crop.stage, "fully grown");

            float restBefore = state.CurrentFarm.restoration;
            Assert.IsTrue(farming.Harvest(7, 7), "harvest");
            Assert.AreEqual(def.sellPrice, state.Save.coins, "coins from sale");
            Assert.AreEqual(def.xp, state.Save.xp, "xp granted");
            float expectedRestore = def.restore * (def.native ? 1.5f : 1f);
            Assert.AreEqual(restBefore + expectedRestore, state.CurrentFarm.restoration, 0.001f,
                "restoration gain (native bonus)");
            Assert.IsNull(farming.PlotAt(7, 7).crop, "plot empty after harvest");
            Debug.Log($"[P3Test] full loop OK: coins={state.Save.coins} xp={state.Save.xp} restoration={state.CurrentFarm.restoration}");
        }

        [Test]
        public void Plant_RejectsCropFromAnotherRegion()
        {
            var state = NewState("starlight_vale");
            var farming = new FarmingSystem(state);
            var sunCrop = GameDatabase.CropsFor("sun_garden").GetEnumerator();
            Assert.IsTrue(sunCrop.MoveNext());
            string sunCropId = sunCrop.Current.id;

            state.Save.seeds[sunCropId] = 1;
            Assert.IsTrue(farming.Till(7, 7));
            Assert.IsFalse(farming.Plant(7, 7, sunCropId),
                $"{sunCropId} (sun_garden) must not plant in starlight_vale");

            // same crop in its home region works
            var sunState = NewState("sun_garden");
            var sunFarming = new FarmingSystem(sunState);
            sunState.Save.seeds[sunCropId] = 1;
            Assert.IsTrue(sunFarming.Till(7, 7));
            Assert.IsTrue(sunFarming.Plant(7, 7, sunCropId), $"{sunCropId} plants in sun_garden");
            Debug.Log($"[P3Test] region gate OK for {sunCropId}");
        }

        [Test]
        public void Water_GatesEveryStage()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);
            state.Save.seeds["starlight_berry"] = 1;
            farming.Till(6, 6);
            farming.Plant(6, 6, "starlight_berry");
            var plot = farming.PlotAt(6, 6);

            Assert.IsTrue(plot.crop.needsWater, "needs water at stage 0");
            ForceGrowStage(state, farming, 6, 6);
            Assert.AreEqual(0, plot.crop.stage, "no growth while thirsty");

            farming.Water(6, 6);
            ForceGrowStage(state, farming, 6, 6);
            Assert.AreEqual(1, plot.crop.stage, "grew after watering");
            Assert.IsTrue(plot.crop.needsWater, "next stage needs fresh water");
        }

        [UnityTest]
        public IEnumerator FarmGrid_TapInteraction_TwoPlotsInTwoRegions()
        {
            GameEvents.Clear();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = NewState("starlight_vale", coins: 500);
            var farming = new FarmingSystem(state);
            regions.OnRegionChanged += id => { state.Save.currentRegion = id; };

            foreach (var kv in grids)
                FarmGrid.Create(kv.Key, kv.Value, farming, state);
            var player = PlayerController.Spawn(new Vector3(20, 20));
            yield return new WaitForFixedUpdate();   // colliders

            // --- plot A in starlight_vale at cell (16,16) → world (16.5,16.5)
            yield return DriveLoop(player, farming, state, new Vector3(16.5f, 16.5f), 16, 16, "starlight_vale");

            // --- travel to sun_garden (region index 1 → origin x=80), plot B cell (14,14)
            regions.SetCurrentRegion("sun_garden");
            player.transform.position = new Vector3(100, 20);
            yield return DriveLoop(player, farming, state, new Vector3(94.5f, 14.5f), 14, 14, "sun_garden");

            Object.DestroyImmediate(player.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        IEnumerator DriveLoop(PlayerController player, FarmingSystem farming, GameState state,
                              Vector3 tapWorld, int x, int y, string region)
        {
            // walk into range first so taps interact immediately
            player.transform.position = tapWorld + new Vector3(0.8f, 0f);

            player.OnTapWorld(tapWorld);   // till
            var plot = farming.PlotAt(x, y);
            Assert.IsNotNull(plot, $"{region}({x},{y}) plot created by tap");
            Assert.IsTrue(plot.tilled, $"{region}({x},{y}) tilled by tap");

            player.OnTapWorld(tapWorld);   // plant (selected crop auto-bought from coins)
            Assert.IsNotNull(plot.crop, $"{region}({x},{y}) planted by tap");
            var def = GameDatabase.Crop(plot.crop.id);
            Assert.AreEqual(region, def.region, "planted crop belongs to its region");
            Debug.Log($"[P3Test] {region}({x},{y}) planted {plot.crop.id}");

            player.OnTapWorld(tapWorld);   // water
            Assert.IsFalse(plot.crop.needsWater, $"{region}({x},{y}) watered by tap");

            for (int guard = 0; plot.crop.stage < def.stages - 1 && guard < 10; guard++)
            {
                ForceGrowStage(state, farming, x, y);
                if (plot.crop != null && plot.crop.needsWater) player.OnTapWorld(tapWorld);
            }

            int coinsBefore = state.Save.coins;
            player.OnTapWorld(tapWorld);   // harvest
            Assert.IsNull(plot.crop, $"{region}({x},{y}) harvested by tap");
            Assert.AreEqual(coinsBefore + def.sellPrice, state.Save.coins, "sale proceeds");
            Debug.Log($"[P3Test] {region}({x},{y}) harvested {def.id}: coins {coinsBefore}→{state.Save.coins}");
            yield return null;
        }
    }
}
