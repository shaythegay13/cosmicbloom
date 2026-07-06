// Phase 5 verification — replaces Phase 4's one-home-region model. Shared
// animals spawn in every region; region-exclusives spawn only in their one
// assigned region and are absent/rejected elsewhere.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class AnimalRegionTests
    {
        static GameState NewState(int coins, int level)
        {
            GameDatabase.LoadAll();
            var s = new GameState(new SaveSnapshot());
            s.Save.coins = coins;
            s.Save.level = level;
            return s;
        }

        [Test]
        public void SharedExclusiveSplit_MatchesSpec()
        {
            GameDatabase.LoadAll();
            var regionIds = GameDatabase.Regions.Select(r => r.id).ToList();
            var animalIds = GameDatabase.Animals.Select(a => a.id).ToList();
            Assert.AreEqual(5, regionIds.Count);
            Assert.AreEqual(15, animalIds.Count);

            int sharedCount = 0;
            var exclusivesPerRegion = regionIds.ToDictionary(r => r, r => 0);

            foreach (var animalId in animalIds)
            {
                var eligible = regionIds.Where(r => AnimalSpawner.CanSpawnInRegion(animalId, r)).ToList();
                if (eligible.Count == regionIds.Count) { sharedCount++; continue; }
                Assert.AreEqual(1, eligible.Count, $"{animalId} must be shared (all 5) or exclusive (exactly 1)");
                exclusivesPerRegion[eligible[0]]++;
            }

            Assert.AreEqual(9, sharedCount, "9 animals are shared across all 5 regions");
            Assert.AreEqual(6, exclusivesPerRegion.Values.Sum(), "6 animals are region-exclusive");
            Assert.AreEqual(2, exclusivesPerRegion["nebula_garden"], "Nebula Garden is last in progression (highest unlockCoins/unlockLevel) — gets the extra exclusive");
            Assert.AreEqual(1, exclusivesPerRegion["starlight_vale"]);
            Assert.AreEqual(1, exclusivesPerRegion["sun_garden"]);
            Assert.AreEqual(1, exclusivesPerRegion["moon_garden"]);
            Assert.AreEqual(1, exclusivesPerRegion["star_garden"]);
        }

        [UnityTest]
        public IEnumerator SharedAnimal_SpawnsInEveryRegion()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = NewState(coins: 100000, level: 20);

            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "chicken"), "buy chicken (shared)");
            yield return null;

            var chickenInstances = AnimalSpawner.Spawned.Where(a => a.Def.id == "chicken").ToList();
            Assert.AreEqual(5, chickenInstances.Count, "shared animal spawns one instance per region");

            var regionIds = new[] { "starlight_vale", "sun_garden", "moon_garden", "star_garden", "nebula_garden" };
            int distinctRegionsContaining = 0;
            foreach (var regionId in regionIds)
            {
                regions.TryGetBounds(regionId, out var bounds);
                if (chickenInstances.Any(a => bounds.Contains(a.transform.position))) distinctRegionsContaining++;
            }
            Assert.GreaterOrEqual(distinctRegionsContaining, 2, "shared animal is spawnable in at least two different regions");
            Assert.AreEqual(5, distinctRegionsContaining, "chicken lands inside all 5 region bounds");
            Debug.Log($"[P5Test] chicken (shared): {chickenInstances.Count} instances across {distinctRegionsContaining} regions");

            Cleanup(regions, grids);
        }

        [UnityTest]
        public IEnumerator ExclusiveAnimal_OnlyInAssignedRegion_RejectedElsewhere()
        {
            Assert.IsFalse(AnimalSpawner.CanSpawnInRegion("crater_goat", "sun_garden"), "crater_goat rejected outside Moon Garden");
            Assert.IsFalse(AnimalSpawner.CanSpawnInRegion("crater_goat", "starlight_vale"), "crater_goat rejected in the Vale");
            Assert.IsTrue(AnimalSpawner.CanSpawnInRegion("crater_goat", "moon_garden"), "crater_goat allowed in its assigned region");

            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = NewState(coins: 100000, level: 20);

            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "crater_goat"), "buy crater_goat (exclusive)");
            yield return null;

            var goatInstances = AnimalSpawner.Spawned.Where(a => a.Def.id == "crater_goat").ToList();
            Assert.AreEqual(1, goatInstances.Count, "exclusive animal spawns exactly once");

            regions.TryGetBounds("moon_garden", out var moonBounds);
            regions.TryGetBounds("sun_garden", out var sunBounds);
            regions.TryGetBounds("starlight_vale", out var valeBounds);
            Assert.IsTrue(moonBounds.Contains(goatInstances[0].transform.position), "crater_goat spawns in Moon Garden");
            Assert.IsFalse(sunBounds.Contains(goatInstances[0].transform.position), "crater_goat absent from Sun Garden");
            Assert.IsFalse(valeBounds.Contains(goatInstances[0].transform.position), "crater_goat absent from the Vale");
            Debug.Log($"[P5Test] crater_goat (exclusive) @ {goatInstances[0].transform.position}, moon_garden bounds {moonBounds}");

            Cleanup(regions, grids);
        }

        static void Cleanup(RegionManager regions, Dictionary<string, Grid> grids)
        {
            // AnimalSpawner._spawned is static — SpawnOwned(empty) both destroys and
            // clears it, so it doesn't leak owned animals into the next test's count.
            AnimalSpawner.SpawnOwned(NewState(coins: 0, level: 0), regions);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }
    }
}
