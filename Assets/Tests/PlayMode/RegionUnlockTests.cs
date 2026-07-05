// Phase 8 verification — region unlock (data-driven coins+level), travel,
// movement confinement to the current region, restoration-driven quest
// progression (the chain step that gates the unlock economy), and the
// unlock_region quest step completing on a real unlock.
using System.Collections;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class RegionUnlockTests
    {
        RegionManager _regions;
        GameState _state;
        System.Collections.Generic.Dictionary<string, Grid> _grids;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _state = new GameState(new SaveSnapshot());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        [Test]
        public void Unlock_RequiresDataCoinsAndLevel()
        {
            var sun = GameDatabase.Region("sun_garden");   // 1500c, Lv8

            Assert.IsFalse(RegionUnlockService.IsUnlocked(_state, "sun_garden"), "locked at start");
            Assert.IsTrue(RegionUnlockService.IsUnlocked(_state, "starlight_vale"), "vale open from start");

            _state.Save.coins = 5000; _state.Save.level = 7;
            Assert.IsFalse(RegionUnlockService.TryUnlock(_state, "sun_garden"), "level 7 < 8 blocks");

            _state.Save.level = 8; _state.Save.coins = 1499;
            Assert.IsFalse(RegionUnlockService.TryUnlock(_state, "sun_garden"), "1499c < 1500 blocks");

            _state.Save.coins = 1600;
            string unlockedEvent = null;
            GameEvents.OnRegionUnlocked += r => unlockedEvent = r;
            Assert.IsTrue(RegionUnlockService.TryUnlock(_state, "sun_garden"), "meets data gate");
            Assert.AreEqual(1600 - sun.unlockCoins, _state.Save.coins, "cost deducted");
            Assert.Contains("sun_garden", _state.Save.unlockedRegions);
            Assert.AreEqual("sun_garden", unlockedEvent, "event for quest engine");
            Debug.Log($"[P8Test] sun_garden unlocked: coins 1600→{_state.Save.coins}, level {_state.Save.level}");
        }

        [UnityTest]
        public IEnumerator Travel_MovesPlayerAndConfinesMovement()
        {
            var player = PlayerController.Spawn(new Vector3(10, 10));
            yield return null;

            Assert.IsFalse(RegionUnlockService.TravelTo(_state, "moon_garden"), "locked region refuses travel");

            _state.Save.coins = 99999; _state.Save.level = 99;
            RegionUnlockService.TryUnlock(_state, "moon_garden");
            Assert.IsTrue(RegionUnlockService.TravelTo(_state, "moon_garden"), "unlocked region travels");
            Assert.AreEqual("moon_garden", _regions.CurrentRegionId);
            Assert.AreEqual(new Vector3(90, 10, 0), player.transform.position, "player at moon center");

            // movement now clamps INSIDE moon_garden (x 80..100), can't walk back to the vale
            player.OnTapWorld(new Vector3(10, 10));
            Assert.GreaterOrEqual(player.MoveTarget.Value.x, 80f,
                "tap toward the vale clamps to moon_garden's west edge");
            Debug.Log($"[P8Test] traveled to moon_garden; cross-region tap clamped to {player.MoveTarget}");

            Object.DestroyImmediate(player.gameObject);
        }

        [UnityTest]
        public IEnumerator RestorationThreshold_DrivesQuestProgress()
        {
            var engine = new GameObject("TestEngine").AddComponent<QuestEngine>();
            engine.Bind(_state);
            UI.DialogueUI.Close();

            // q_first_friend: restoration_above starlight_vale ≥ 25
            engine.StartQuest("q_first_friend");
            Assert.IsTrue(engine.IsActive("q_first_friend"));

            _state.FarmFor("starlight_vale").restoration = 25f;   // "artificially raise" per spec
            float deadline = Time.time + 3f;
            while (engine.IsActive("q_first_friend") && Time.time < deadline) yield return null;

            Assert.IsTrue(engine.IsCompleted("q_first_friend"),
                "restoration 25 completes the restoration_above(25) quest");
            Debug.Log("[P8Test] restoration 25 → q_first_friend completed (chain funds the unlock)");
            Object.DestroyImmediate(engine.gameObject);
        }

        [UnityTest]
        public IEnumerator UnlockRegionQuestStep_CompletesOnRealUnlock()
        {
            var engine = new GameObject("TestEngine").AddComponent<QuestEngine>();
            engine.Bind(_state);
            UI.DialogueUI.Close();

            engine.StartQuest("q_gates_of_sun");   // step: unlock_region sun_garden
            Assert.IsTrue(engine.IsActive("q_gates_of_sun"));

            _state.Save.coins = 2000; _state.Save.level = 8;
            RegionUnlockService.TryUnlock(_state, "sun_garden");
            yield return null;

            Assert.IsTrue(engine.IsCompleted("q_gates_of_sun"),
                "unlock_region step completed by the actual unlock");
            Debug.Log("[P8Test] q_gates_of_sun completed via real sun_garden unlock");
            Object.DestroyImmediate(engine.gameObject);
        }
    }
}
