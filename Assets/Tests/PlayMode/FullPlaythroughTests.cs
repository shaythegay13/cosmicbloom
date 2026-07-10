// Definition-of-done playthrough: start in Starlight Vale → farm & complete
// quests → raise restoration → unlock Sun Garden → travel there → repeat the
// basic loop. Real systems end to end (engine, farming, unlock, travel);
// only the coin/XP grind is accelerated via quest-scale grants, mirroring
// what dozens of repeat harvests would produce.
using System.Collections;
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
            player.transform.position = new Vector3(20, 20);   // vale center after the 40x40 resize
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
            Assert.AreEqual(new Vector3(100, 20, 0), player.transform.position, "standing in Sun Garden");

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

        static IEnumerator WaitWhileActive(QuestEngine engine, string questId, float timeoutSeconds = 3f)
        {
            float deadline = Time.time + timeoutSeconds;
            while (engine.IsActive(questId) && Time.time < deadline) yield return null;
        }

        static IEnumerator WaitWhileStepIs(QuestEngine engine, string questId, int stepIndex, float timeoutSeconds = 3f)
        {
            float deadline = Time.time + timeoutSeconds;
            while (engine.StepIndex(questId) == stepIndex && Time.time < deadline) yield return null;
        }

        // A fresh plot per cycle (baseX+i), not one reused plot — reusing a single
        // plot across a large N drains soilHealth below the 15-to-plant floor after
        // a handful of harvests (found running this test: q_market_day's 5th cycle
        // failed to plant because q_green_thumb's prior 6 reuses of the same plot
        // had already dropped it to soil 12).
        static IEnumerator HarvestNTimes(FarmingSystem farming, GameState state, int baseX, int y, string cropId, int times)
        {
            for (int i = 0; i < times; i++)
            {
                int x = baseX + i;
                if (farming.PlotAt(x, y) == null) farming.Till(x, y);
                state.Save.seeds.TryGetValue(cropId, out var have);
                state.Save.seeds[cropId] = have + 1;
                Assert.IsTrue(farming.Plant(x, y, cropId), $"plant {cropId} cycle {i} at ({x},{y})");
                yield return GrowAndHarvest(farming, state, x, y);
            }
        }

        static void EnsureCoins(GameState state, int amount)
        {
            if (state.Save.coins < amount) state.AddCoins(amount - state.Save.coins);
        }

        static void EnsureLevel(GameState state, int level)
        {
            while (state.Save.level < level) state.AddXp(GameState.XpForLevel(state.Save.level));
        }

        // Phase 7: walks the real 47-quest main chain from q_arrival for real, through
        // every trigger Phase 2/3 resolved: fishing, buy_animal+collect,
        // harvest_at_night, explore, wait_for, deposit_seeds, deliver, craft,
        // cutscenes. Stops cleanly at q_pebbles_past (#23) — one quest short of
        // q_trade_routes (#24).
        //
        // q_trade_routes ("sell 10 crops of at least 3 kinds") is NOT walked here.
        // A Phase 7 first pass "fixed" its sell_distinct step to require 10 total
        // sales across >=3 kinds, on the assumption that 10 DISTINCT kinds was an
        // impossible ask (no single region has that many crops). That assumption
        // was wrong and the fix was reverted (Phase 8 pre-check, 2026-07-06): region
        // unlocking (RegionMapUI -> RegionUnlockService.TryUnlock) is gated only by
        // level+coins, NOT by quest progress, and the distinct-kind tracking never
        // resets on travel — so a player who unlocks a second region early (vale's
        // 6 crops + any other region's 5 = 11) genuinely can reach 10 distinct
        // kinds. It's a real, if backward, cross-region requirement, not a bug.
        //
        // It's also not safely walkable here as a shortcut: unlocking sun_garden
        // early to farm this quest would make q_gates_of_sun's own unlock_region
        // step (which only fires GameEvents.RegionUnlocked on an actual state
        // transition) never complete later, since TryUnlock no-ops once already
        // unlocked. That's a second, separate finding — flagged for the same
        // design call rather than papered over. See BUILD_LOG.md for both.
        [UnityTest]
        public IEnumerator MainChain_ArrivalThroughPebblesPast_AllRealQuestSteps()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("World7").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            var farming = new FarmingSystem(state);
            regions.OnRegionChanged += id => { state.Save.currentRegion = id; };
            foreach (var kv in grids) FarmGrid.Create(kv.Key, kv.Value, farming, state);
            var time = new GameObject("Time7").AddComponent<TimeManager>();
            var engine = new GameObject("Engine7").AddComponent<QuestEngine>();
            engine.Bind(state);
            var player = PlayerController.Spawn(new Vector3(2, 2));
            yield return new WaitForFixedUpdate();

            // ── q_arrival (1): cutscene + move_to starting_plot
            UI.DialogueUI.Close();
            player.transform.position = new Vector3(20, 20);   // vale center after the 40x40 resize
            yield return WaitWhileActive(engine, "q_arrival");
            Assert.IsTrue(engine.IsCompleted("q_arrival"));
            UI.DialogueUI.Close();

            // ── q_first_sprout (2): clear 3 (of 5 tilled) / plant 5 berries / harvest 1
            for (int i = 0; i < 5; i++) farming.Till(i, 50);
            state.Save.seeds["starlight_berry"] = 5;
            for (int i = 0; i < 5; i++) Assert.IsTrue(farming.Plant(i, 50, "starlight_berry"));
            yield return GrowAndHarvest(farming, state, 0, 50);
            Assert.IsTrue(engine.IsCompleted("q_first_sprout"));
            UI.DialogueUI.Close();

            // ── q_soil_secrets (3): plant 2 clover on fresh (soil 100) plots — the
            // ≥60 threshold poll is already satisfied by any 2 tilled plots
            farming.Till(5, 50); farming.Till(6, 50);
            state.Save.seeds["clover_cover"] = 2;
            Assert.IsTrue(farming.Plant(5, 50, "clover_cover"));
            Assert.IsTrue(farming.Plant(6, 50, "clover_cover"));
            yield return WaitWhileActive(engine, "q_soil_secrets");
            Assert.IsTrue(engine.IsCompleted("q_soil_secrets"));
            UI.DialogueUI.Close();

            // ── q_green_thumb (4): harvest_any x10 (4 already-growing berries + 6 fresh cycles)
            for (int i = 1; i <= 4; i++) yield return GrowAndHarvest(farming, state, i, 50);
            yield return HarvestNTimes(farming, state, 20, 50, "starlight_berry", 6);
            Assert.IsTrue(engine.IsCompleted("q_green_thumb"));
            UI.DialogueUI.Close();

            // ── q_market_day (5): sell_any x5 (harvest sells automatically)
            yield return HarvestNTimes(farming, state, 30, 50, "starlight_berry", 5);
            Assert.IsTrue(engine.IsCompleted("q_market_day"));
            UI.DialogueUI.Close();

            // ── q_moonlit_pond (6): restore_landmark moonlit_pond (real restore-cost Interact)
            EnsureCoins(state, 400);
            var pond = LandmarkController.Spawn(
                GameDatabase.Region("starlight_vale").landmarks.First(l => l.id == "moonlit_pond"),
                "starlight_vale", new Vector3(3, 3), state);
            pond.Interact(player);
            Assert.IsTrue(engine.IsCompleted("q_moonlit_pond"), "real restore-cost Interact fires the event");
            UI.DialogueUI.Close();
            Assert.Contains("fishing_rod", state.Save.tools, "fishing rod granted");

            // ── q_first_catch (7): catch_fish x3 at the just-restored pond
            for (int i = 0; i < 3; i++) { pond.Interact(player); UI.DialogueUI.Close(); }
            Assert.IsTrue(engine.IsCompleted("q_first_catch"));

            // level top-up once, covers chicken(L3), bee_swarm(L8), sun_garden(L8+1500c) —
            // same quest-scale acceleration the original playthrough test used for coins/xp
            EnsureLevel(state, 8);

            // ── q_coop_raising (8): buy_animal chicken
            int animalIdx = AnimalSpawner.Spawned.Count;
            EnsureCoins(state, 200);
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "chicken"));
            Assert.IsTrue(engine.IsCompleted("q_coop_raising"));
            var chicken = AnimalSpawner.Spawned.Skip(animalIdx).First(a => a.Def.id == "chicken");

            // ── q_egg_basket (9): collect glowing_egg x5
            for (int i = 0; i < 5; i++)
            {
                chicken.ForceProduceReady();
                chicken.Interact(player);
                UI.DialogueUI.Close();
            }
            Assert.IsTrue(engine.IsCompleted("q_egg_basket"));

            // ── q_night_shift (10): harvest_at_night x5
            time.timeOfDay = 0.9f;   // night
            yield return HarvestNTimes(farming, state, 40, 50, "starlight_berry", 5);
            Assert.IsTrue(engine.IsCompleted("q_night_shift"));
            UI.DialogueUI.Close();

            // ── q_first_friend (11): restoration_above(vale, 25) — already true from ~25 harvests
            yield return WaitWhileActive(engine, "q_first_friend");
            Assert.IsTrue(engine.IsCompleted("q_first_friend"));
            Assert.Contains("biolume_fox", state.Save.animals, "pet_ prefix stripped by GrantItem");
            UI.DialogueUI.Close();

            // ── q_pollinator_pals (12): buy_animal bee_swarm
            EnsureCoins(state, 600);
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "bee_swarm"));
            Assert.IsTrue(engine.IsCompleted("q_pollinator_pals"));
            UI.DialogueUI.Close();

            // ── q_broken_glass (13): restore_landmark broken_greenhouse
            EnsureCoins(state, 250);
            var greenhouse = LandmarkController.Spawn(
                GameDatabase.Region("starlight_vale").landmarks.First(l => l.id == "broken_greenhouse"),
                "starlight_vale", new Vector3(4, 4), state);
            greenhouse.Interact(player);
            Assert.IsTrue(engine.IsCompleted("q_broken_glass"));
            UI.DialogueUI.Close();

            // ── q_vale_blooms (14): restoration_above(vale, 50) — already true
            yield return WaitWhileActive(engine, "q_vale_blooms");
            Assert.IsTrue(engine.IsCompleted("q_vale_blooms"));
            UI.DialogueUI.Close();

            // ── q_whispers (15): cutscene — end of Act 1
            UI.DialogueUI.Close();
            Assert.IsTrue(engine.IsCompleted("q_whispers"));
            Debug.Log($"[P7Test] Act 1 complete: coins={state.Save.coins} level={state.Save.level} " +
                      $"vale restoration={state.FarmFor("starlight_vale").restoration:F1}");

            // ── q_journals (16): collect clan_journal x3
            PickupSpawner.Sync(state, regions, engine);
            yield return null;
            var journals = Object.FindObjectsByType<PickupController>()
                .Where(p => p.ItemId == "clan_journal").ToList();
            Assert.AreEqual(3, journals.Count);
            foreach (var j in journals) { j.Interact(player); UI.DialogueUI.Close(); }
            Assert.IsTrue(engine.IsCompleted("q_journals"));

            // ── q_lost_seed (17): explore crystal_cave -> collect rare_seed x1
            Assert.IsTrue(ExploreTargets.TryGetPosition("crystal_cave", out var cave));
            player.transform.position = cave;
            yield return WaitWhileStepIs(engine, "q_lost_seed", 0);
            PickupSpawner.Sync(state, regions, engine);
            yield return null;
            var seed = Object.FindObjectsByType<PickupController>()
                .First(p => p.ItemId == "rare_seed");
            seed.Interact(player); UI.DialogueUI.Close();
            Assert.IsTrue(engine.IsCompleted("q_lost_seed"));

            // ── q_crystal_depths (18): explore crystal_cave_depths -> collect prism_shard x3
            Assert.IsTrue(ExploreTargets.TryGetPosition("crystal_cave_depths", out var depths));
            player.transform.position = depths;
            yield return WaitWhileStepIs(engine, "q_crystal_depths", 0);
            PickupSpawner.Sync(state, regions, engine);
            yield return null;
            var shards = Object.FindObjectsByType<PickupController>()
                .Where(p => p.ItemId == "prism_shard").ToList();
            Assert.AreEqual(3, shards.Count);
            foreach (var s in shards) { s.Interact(player); UI.DialogueUI.Close(); }
            Assert.IsTrue(engine.IsCompleted("q_crystal_depths"));

            // ── q_meteor_mystery (19): wait_for meteor_shower -> explore crash_site
            GameEvents.MeteorShower();
            Assert.IsTrue(ExploreTargets.TryGetPosition("crash_site", out var crash));
            player.transform.position = crash;
            yield return WaitWhileActive(engine, "q_meteor_mystery");
            Assert.IsTrue(engine.IsCompleted("q_meteor_mystery"));

            // ── q_crash_garden (20): plant 5 potato / harvest 5 potato
            state.Save.seeds["potato"] = 5;
            for (int i = 10; i < 15; i++) { farming.Till(i, 50); Assert.IsTrue(farming.Plant(i, 50, "potato")); }
            for (int i = 10; i < 15; i++) yield return GrowAndHarvest(farming, state, i, 50);
            Assert.IsTrue(engine.IsCompleted("q_crash_garden"));
            UI.DialogueUI.Close();

            // ── q_arboretum (21): restore_landmark celestial_arboretum
            EnsureCoins(state, 900);
            var arboretum = LandmarkController.Spawn(
                GameDatabase.Region("starlight_vale").landmarks.First(l => l.id == "celestial_arboretum"),
                "starlight_vale", new Vector3(5, 5), state);
            arboretum.Interact(player);
            Assert.IsTrue(engine.IsCompleted("q_arboretum"));
            UI.DialogueUI.Close();

            // ── q_seed_vault (22): deposit_seeds kinds:3 count:9 at the restored arboretum
            state.Save.seeds["starlight_berry"] = 4;
            state.Save.seeds["clover_cover"] = 3;
            state.Save.seeds["potato"] = 2;
            arboretum.Interact(player);
            Assert.IsTrue(engine.IsCompleted("q_seed_vault"));
            UI.DialogueUI.Close();

            // ── q_pebbles_past (23): deliver sealed_crate -> elder_nova (item auto-granted on activation)
            Assert.Contains("sealed_crate", state.Save.heldItems);
            GameEvents.TalkedTo("elder_nova");
            Assert.IsTrue(engine.IsCompleted("q_pebbles_past"));

            // ── q_trade_routes (24) is the real frontier — see the class-level
            // comment above this test for why it stops here pending a design call.
            Assert.IsTrue(engine.IsActive("q_trade_routes"), "chain reaches q_trade_routes for real");

            Debug.Log($"[P7Test] DONE — real quest chain walked through #23 (q_pebbles_past). " +
                      $"Completed=[{string.Join(",", state.Save.questsCompleted)}]");
            Assert.GreaterOrEqual(state.Save.questsCompleted.Count, 23,
                "at least the 23 main-chain quests through q_pebbles_past completed");

            PickupSpawner.ClearAll();
            ExploreMarkerSpawner.ClearAll();
            AnimalSpawner.ClearAll();
            Object.DestroyImmediate(pond.gameObject);
            Object.DestroyImmediate(greenhouse.gameObject);
            Object.DestroyImmediate(arboretum.gameObject);
            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(engine.gameObject);
            Object.DestroyImmediate(time.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }
    }
}
