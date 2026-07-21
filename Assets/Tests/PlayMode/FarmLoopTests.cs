// Phase 3 verification (reworked P3-Phase 17 for free-form placement) — the
// generic farm loop with real assertions:
//  * full till→plant→water→grow→harvest against save-backed PlotState
//  * region-appropriateness (crops.json region field is enforced)
//  * watering gates every stage
//  * free-form tap-to-till: tapping bare ground creates a plot on demand, then
//    the same cell plants/waters/harvests — driven at a WILD-tier cell far from
//    the old fixed 256-cell core (P3-Phase 17)
//  * ground tier is a soil-quality modifier, not a placement gate
//  * the per-farm plot count is capped to the server's limit
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

        [Test]
        public void Till_StartingSoilVariesByGroundTier()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);

            // (20,20) lush core, (8,8) mid ring, (3,3) wild outer ring
            Assert.AreEqual(2, RegionWorldBuilder.TierAt(20, 20));
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(8, 8));
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(3, 3));

            farming.Till(20, 20);
            farming.Till(8, 8);
            farming.Till(3, 3);

            float lush = farming.PlotAt(20, 20).soilHealth;
            float mid = farming.PlotAt(8, 8).soilHealth;
            float wild = farming.PlotAt(3, 3).soilHealth;
            Assert.Greater(lush, mid, "lush core starts with better soil than the mid ring");
            Assert.Greater(mid, wild, "mid ring starts with better soil than wild ground");
            Assert.Greater(wild, FarmingSystem.SoilFloorToPlant,
                "even wild ground is workable — tillable everywhere, just not equally good");
            Debug.Log($"[P17Test] starting soil by tier: lush={lush} mid={mid} wild={wild}");
        }

        [Test]
        public void Till_StopsAtServerPlotCap()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);
            var farm = state.CurrentFarm;

            // fill straight to the cap with synthetic plots, then confirm one more is refused
            for (int i = 0; i < FarmingSystem.MaxPlotsPerFarm; i++)
                farm.plots[$"cap{i}"] = new PlotState { tilled = true };
            Assert.AreEqual(FarmingSystem.MaxPlotsPerFarm, farm.plots.Count);

            Assert.IsFalse(farming.Till(2, 2), "till refused at the per-farm cap (unsyncable-save guard)");
            Assert.AreEqual(FarmingSystem.MaxPlotsPerFarm, farm.plots.Count, "no plot created past the cap");
            Debug.Log($"[P17Test] plot cap {FarmingSystem.MaxPlotsPerFarm} enforced (matches server validateSnapshot)");
        }

        [Test]
        public void IsTillable_ExcludesReservedAndOutOfBoundsCells()
        {
            // wild/mid/lush ground is all tillable ((28,20) is mid ring, clear of any
            // landmark footprint; (8,8) sits under a corner landmark so isn't a fair pick)
            Assert.IsTrue(RegionWorldBuilder.IsTillable(3, 3), "wild ground tillable");
            Assert.AreEqual(1, RegionWorldBuilder.TierAt(28, 20), "(28,20) is mid ring");
            Assert.IsTrue(RegionWorldBuilder.IsTillable(28, 20), "mid ground tillable");
            Assert.IsTrue(RegionWorldBuilder.IsTillable(20, 20), "lush core tillable");

            // out of bounds is not
            Assert.IsFalse(RegionWorldBuilder.IsTillable(-1, 5), "negative cell not tillable");
            Assert.IsFalse(RegionWorldBuilder.IsTillable(RegionWorldBuilder.Size, 5), "past-edge cell not tillable");

            // the social plaza (center + (0,-11) → cell ~(20,9)) is reserved
            Assert.IsTrue(ReservedTiles.IsReserved(20, 9), "NPC social plaza reserved");
            Assert.IsFalse(RegionWorldBuilder.IsTillable(20, 9), "can't till the plaza");

            // a landmark slot (center + (0,14) → cell (20,34)) is reserved
            Assert.IsTrue(ReservedTiles.IsReserved(20, 34), "north landmark slot reserved");
        }

        [UnityTest]
        public IEnumerator FreeForm_TapTillsBareGround_ThenFullLoop_AtWildCell()
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
            yield return new WaitForFixedUpdate();

            // A WILD-tier cell far from the old 256-cell lush core (12..27): cell (3,3).
            const int x = 3, y = 3;
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(x, y), "(3,3) is wild — outside the old fixed grid");
            Assert.IsNull(farming.PlotAt(x, y), "no plot there yet");
            Assert.IsNull(GameObject.Find($"Plot_{x}_{y}"), "no eager view there");

            var tapWorld = new Vector3(x + 0.5f, y + 0.5f, 0f);
            yield return DriveLoop(player, farming, state, tapWorld, x, y, "starlight_vale");

            Object.DestroyImmediate(player.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }

        IEnumerator DriveLoop(PlayerController player, FarmingSystem farming, GameState state,
                              Vector3 tapWorld, int x, int y, string region)
        {
            // stand within interact range so each tap acts immediately
            player.transform.position = tapWorld + new Vector3(0.8f, 0f);

            player.OnTapWorld(tapWorld);          // till bare ground → creates a plot on demand
            yield return new WaitForFixedUpdate();   // let the new FarmPlot collider register
            var plot = farming.PlotAt(x, y);
            Assert.IsNotNull(plot, $"{region}({x},{y}) plot created by tapping bare ground");
            Assert.IsTrue(plot.tilled, $"{region}({x},{y}) tilled by tap");
            Assert.IsNotNull(GameObject.Find($"Plot_{x}_{y}"), "a plot view was materialised on demand");

            player.OnTapWorld(tapWorld);          // plant (selected crop auto-bought from coins)
            Assert.IsNotNull(plot.crop, $"{region}({x},{y}) planted by tap");
            var def = GameDatabase.Crop(plot.crop.id);
            Assert.AreEqual(region, def.region, "planted crop belongs to its region");
            Debug.Log($"[P17Test] {region}({x},{y}) [tier {RegionWorldBuilder.TierAt(x, y)}] planted {plot.crop.id}");

            player.OnTapWorld(tapWorld);          // water
            Assert.IsFalse(plot.crop.needsWater, $"{region}({x},{y}) watered by tap");

            for (int guard = 0; plot.crop.stage < def.stages - 1 && guard < 10; guard++)
            {
                ForceGrowStage(state, farming, x, y);
                if (plot.crop != null && plot.crop.needsWater) player.OnTapWorld(tapWorld);
            }

            int coinsBefore = state.Save.coins;
            player.OnTapWorld(tapWorld);          // harvest
            Assert.IsNull(plot.crop, $"{region}({x},{y}) harvested by tap");
            Assert.AreEqual(coinsBefore + def.sellPrice, state.Save.coins, "sale proceeds");
            Debug.Log($"[P17Test] {region}({x},{y}) harvested {def.id}: coins {coinsBefore}→{state.Save.coins}");
            yield return null;
        }
    }
}
