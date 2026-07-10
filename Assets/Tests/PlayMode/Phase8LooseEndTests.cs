// Phase 8 (loose ends) verification — the Phase 4b flagged economy issue:
// a shared animal spawns an instance in every owned region, but owning it is
// one purchase, so it must yield exactly one produce stream. The species-wide
// produce clock in AnimalController is the fix; this proves income is 1x.
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class Phase8LooseEndTests
    {
        static GameState NewState(int coins = 1000)
        {
            GameDatabase.LoadAll();
            var s = new GameState(new SaveSnapshot());
            s.Save.coins = coins;
            return s;
        }

        [UnityTest]
        public IEnumerator SharedAnimal_OneProduceStreamAcrossRegions()
        {
            GameEvents.Clear();
            AnimalSpawner.ClearAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            regions.RegisterRegion("starlight_vale", new Bounds(new Vector3(20, 20), new Vector3(40, 40)));
            regions.RegisterRegion("sun_garden", new Bounds(new Vector3(100, 20), new Vector3(40, 40)));
            var state = NewState();
            state.Save.level = 10;

            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "chicken"), "buy shared animal");
            Assert.AreEqual(2, AnimalSpawner.Spawned.Count, "shared animal spawns one instance per registered region");
            var vale = AnimalSpawner.Spawned[0];
            var sun = AnimalSpawner.Spawned[1];
            yield return null;

            Assert.IsFalse(vale.ProduceReady, "fresh clock — not ready at spawn");

            vale.ForceProduceReady();
            Assert.IsTrue(sun.ProduceReady, "species clock is shared — the other instance reads ready too");

            var player = PlayerController.Spawn(vale.transform.position + new Vector3(0.5f, 0f));
            int before = state.Save.coins;
            vale.Interact(player);
            UI.DialogueUI.Close();
            int gained = state.Save.coins - before;
            Assert.Greater(gained, 0, "first collect pays");
            Assert.IsFalse(sun.ProduceReady, "collecting anywhere consumes THE produce and reschedules");

            sun.Interact(player);
            UI.DialogueUI.Close();
            Assert.AreEqual(before + gained, state.Save.coins,
                "second instance pays nothing — income is 1x per owned animal, not per region");
            Debug.Log($"[P8Test] shared-animal income: one stream (+{gained}c once), 2nd instance petted only");

            Object.DestroyImmediate(player.gameObject);
            AnimalSpawner.ClearAll();
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }
    }
}
