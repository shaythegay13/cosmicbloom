// Definition-of-done playthrough: start in Starlight Vale → farm & complete
// quests → raise restoration → unlock Sun Garden → travel there → repeat the
// basic loop. Real systems end to end (engine, farming, unlock, travel);
// only the coin/XP grind is accelerated via quest-scale grants, mirroring
// what dozens of repeat harvests would produce.
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
    public class FullPlaythroughTests
    {
        [UnityTest]
        public IEnumerator ValeToSunGarden_CoreLoop()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("World").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            var farming = new FarmingSystem(state);
            regions.OnRegionChanged += id => { state.Save.currentRegion = id; };
            foreach (var kv in grids) FarmGrid.Create(kv.Key, kv.Value, farming, state);
            var engine = new GameObject("Engine").AddComponent<QuestEngine>();
            engine.Bind(state);
            var player = PlayerController.Spawn(new Vector3(2, 2));
            yield return new WaitForFixedUpdate();

            // ── ACT: arrive (quest 1)
            UI.DialogueUI.Close();
            player.transform.position = new Vector3(10, 10);
            float t = Time.time + 3f;
            while (engine.IsActive("q_arrival") && Time.time < t) yield return null;
            Assert.IsTrue(engine.IsCompleted("q_arrival"), "arrived in the Vale");
            UI.DialogueUI.Close();

            // ── FARM: quest 2 via real tap-driven farming
            state.Save.seeds["starlight_berry"] = 6;
            for (int i = 0; i < 5; i++) farming.Till(6 + i, 7);
            for (int i = 0; i < 5; i++) farming.Plant(6 + i, 7, "starlight_berry");
            yield return GrowAndHarvest(farming, state, 6, 7);
            Assert.IsTrue(engine.IsCompleted("q_first_sprout"), "first sprout done");
            UI.DialogueUI.Close();

            // ── RESTORE: harvest until vale restoration ≥ 25 (native ×1.5 = 4.5/harvest)
            for (int i = 1; i < 5 && state.FarmFor("starlight_vale").restoration < 25f; i++)
                yield return GrowAndHarvest(farming, state, 6 + i, 7);
            while (state.FarmFor("starlight_vale").restoration < 25f)
            {
                state.Save.seeds["starlight_berry"] = 1;
                farming.Till(6, 8); farming.Plant(6, 8, "starlight_berry");
                yield return GrowAndHarvest(farming, state, 6, 8);
            }
            float valeRestoration = state.FarmFor("starlight_vale").restoration;
            Assert.GreaterOrEqual(valeRestoration, 25f, "vale meaningfully restored");
            Debug.Log($"[Playthrough] vale restoration={valeRestoration:F1} coins={state.Save.coins} xp-level={state.Save.level}");

            // ── PROGRESS: quest-scale reward acceleration to the unlock economy
            state.AddCoins(1500 - state.Save.coins + 100);
            while (state.Save.level < 8) state.AddXp(GameState.XpForLevel(state.Save.level));

            // ── UNLOCK + TRAVEL: the real data-driven gate
            Assert.IsTrue(RegionUnlockService.TryUnlock(state, "sun_garden"), "paid 1500c at Lv8");
            Assert.IsTrue(RegionUnlockService.TravelTo(state, "sun_garden"), "traveled");
            Assert.AreEqual("sun_garden", regions.CurrentRegionId);
            Assert.AreEqual(new Vector3(50, 10, 0), player.transform.position, "standing in Sun Garden");

            // ── REPEAT the loop in the new region with its own crop
            var sunCrop = System.Linq.Enumerable.First(GameDatabase.CropsFor("sun_garden"));
            state.Save.seeds[sunCrop.id] = 1;
            Assert.IsTrue(farming.Till(8, 8), "till in sun garden");
            Assert.IsTrue(farming.Plant(8, 8, sunCrop.id), "plant sun-native crop");
            yield return GrowAndHarvest(farming, state, 8, 8);
            Assert.Greater(state.FarmFor("sun_garden").restoration, 0f, "sun garden restoration begun");

            Debug.Log($"[Playthrough] DONE — vale {valeRestoration:F1}% | sun {state.FarmFor("sun_garden").restoration:F1}% | " +
                      $"coins={state.Save.coins} level={state.Save.level} quests={state.Save.questsCompleted.Count} " +
                      $"unlocked=[{string.Join(",", state.Save.unlockedRegions)}]");

            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(engine.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }

        static IEnumerator GrowAndHarvest(FarmingSystem farming, GameState state, int x, int y)
        {
            var plot = farming.PlotAt(x, y);
            if (plot?.crop == null) yield break;
            var def = GameDatabase.Crop(plot.crop.id);
            if (plot.crop.needsWater) farming.Water(x, y);
            for (int guard = 0; plot.crop != null && plot.crop.stage < def.stages - 1 && guard < 12; guard++)
            {
                plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - def.GrowMsPerStage - 1;
                farming.FastForward();
                if (plot.crop != null && plot.crop.needsWater) farming.Water(x, y);
            }
            Assert.IsTrue(farming.Harvest(x, y), $"harvest at ({x},{y})");
            UI.DialogueUI.Close();   // any quest-completion chatter
            yield return null;
        }
    }
}
