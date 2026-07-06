// Phase 4 verification — animals spawn in their assigned home region
// (AnimalSpawner.HomeRegionByAnimal) instead of always the Vale paddock.
using System.Collections;
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

        [UnityTest]
        public IEnumerator NonValeAnimals_SpawnInAssignedRegionBounds()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = NewState(coins: 100000, level: 20);

            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "crater_goat"), "buy crater_goat");
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "asteroid_pig"), "buy asteroid_pig");
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "star_owl"), "buy star_owl");
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "nebula_cow"), "buy nebula_cow");
            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "chicken"), "buy chicken");
            yield return null;

            regions.TryGetBounds("starlight_vale", out var valeBounds);
            regions.TryGetBounds("moon_garden", out var moonBounds);
            regions.TryGetBounds("sun_garden", out var sunBounds);
            regions.TryGetBounds("star_garden", out var starBounds);
            regions.TryGetBounds("nebula_garden", out var nebulaBounds);

            var spawned = AnimalSpawner.Spawned;
            var goat = spawned.First(a => a.Def.id == "crater_goat");
            var pig = spawned.First(a => a.Def.id == "asteroid_pig");
            var owl = spawned.First(a => a.Def.id == "star_owl");
            var cow = spawned.First(a => a.Def.id == "nebula_cow");
            var chicken = spawned.First(a => a.Def.id == "chicken");

            Assert.IsTrue(moonBounds.Contains(goat.transform.position), "crater_goat spawns in moon_garden");
            Assert.IsFalse(valeBounds.Contains(goat.transform.position), "crater_goat is not in the Vale paddock");

            Assert.IsTrue(sunBounds.Contains(pig.transform.position), "asteroid_pig spawns in sun_garden");
            Assert.IsTrue(starBounds.Contains(owl.transform.position), "star_owl spawns in star_garden");
            Assert.IsTrue(nebulaBounds.Contains(cow.transform.position), "nebula_cow spawns in nebula_garden");
            Assert.IsTrue(valeBounds.Contains(chicken.transform.position), "chicken still spawns in starlight_vale");

            Debug.Log($"[P4Test] crater_goat @ {goat.transform.position}, moon_garden bounds {moonBounds}");
            Debug.Log($"[P4Test] asteroid_pig @ {pig.transform.position}, sun_garden bounds {sunBounds}");
            Debug.Log($"[P4Test] star_owl @ {owl.transform.position}, star_garden bounds {starBounds}");
            Debug.Log($"[P4Test] nebula_cow @ {cow.transform.position}, nebula_garden bounds {nebulaBounds}");

            // AnimalSpawner._spawned is static — SpawnOwned(empty) both destroys and
            // clears it, so it doesn't leak owned animals into the next test's count.
            AnimalSpawner.SpawnOwned(NewState(coins: 0, level: 0), regions);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }
    }
}
