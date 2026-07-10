// Phase-2 Spec / Phase 2 verification — the nine QUEST_GAP_REPORT.md BUILD
// recommendations plus the two side-quest-inventory fixes (q_toolsmith,
// q_eternal_bloom), each driven through its real trigger, not just an
// evaluator existing. Mirrors QuestEngineTests/RegionUnlockTests' SetUp shape.
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
    public class QuestGapPhase2Tests
    {
        GameState _state;
        FarmingSystem _farming;
        QuestEngine _engine;
        RegionManager _regions;
        PlayerController _player;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _state = new GameState(new SaveSnapshot());
            _farming = new FarmingSystem(_state);
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _regions.RegisterRegion("starlight_vale", new Bounds(new Vector3(20, 20), new Vector3(40, 40)));
            _player = PlayerController.Spawn(new Vector3(2, 2));
            _engine = new GameObject("TestQuestEngine").AddComponent<QuestEngine>();
            _engine.Bind(_state);
            UI.DialogueUI.Close();

            // mirror GameBootstrap's reactive world-content sync (no GameBootstrap in tests)
            _engine.OnQuestStarted += _ => Sync();
            _engine.OnProgressChanged += Sync;
        }

        void Sync()
        {
            PickupSpawner.Sync(_state, _regions, _engine);
            ExploreMarkerSpawner.Sync(_engine);
        }

        [TearDown]
        public void TearDown()
        {
            PickupSpawner.ClearAll();
            ExploreMarkerSpawner.ClearAll();
            Object.DestroyImmediate(_engine.gameObject);
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        LandmarkController SpawnRestoredLandmark(string landmarkId)
        {
            var def = GameDatabase.Region("starlight_vale").landmarks.First(l => l.id == landmarkId);
            var lm = LandmarkController.Spawn(def, "starlight_vale", new Vector3(3, 3), _state);
            _state.FarmFor("starlight_vale").landmarksRestored.Add(landmarkId);
            return lm;
        }

        [Test]
        public void CatchFish_CompletesQFirstCatch()
        {
            var pond = SpawnRestoredLandmark("moonlit_pond");
            _state.Save.tools.Add("fishing_rod");
            _engine.StartQuest("q_first_catch");   // catch_fish x3
            UI.DialogueUI.Close();

            int coinsBefore = _state.Save.coins;
            for (int i = 0; i < 3; i++) { pond.Interact(_player); UI.DialogueUI.Close(); }

            Assert.IsTrue(_engine.IsCompleted("q_first_catch"), "3 catches complete the step");
            Assert.Greater(_state.Save.coins, coinsBefore, "fishing + quest reward coins");
            Object.DestroyImmediate(pond.gameObject);
            Debug.Log($"[P2Test] q_first_catch completed via 3 real pond interactions");
        }

        [Test]
        public void FishingWithoutRod_ShowsMessageAndDoesNotProgress()
        {
            var pond = SpawnRestoredLandmark("moonlit_pond");
            _engine.StartQuest("q_first_catch");
            UI.DialogueUI.Close();

            pond.Interact(_player);
            Assert.AreEqual(0, _engine.StepProgress("q_first_catch"), "no rod, no progress");
            Object.DestroyImmediate(pond.gameObject);
        }

        [UnityTest]
        public IEnumerator WorldPickups_SpawnAndCollect_CompletesQJournals()
        {
            _engine.StartQuest("q_journals");   // collect clan_journal x3
            UI.DialogueUI.Close();
            yield return null;

            var pickups = Object.FindObjectsByType<PickupController>()
                .Where(p => p.ItemId == "clan_journal").ToList();
            Assert.AreEqual(3, pickups.Count, "3 clan_journal pickups spawned for the collect step");

            foreach (var p in pickups)
            {
                p.Interact(_player);
                UI.DialogueUI.Close();
            }

            Assert.IsTrue(_engine.IsCompleted("q_journals"), "collecting all 3 completes q_journals");
            yield return null;   // Object.Destroy (PickupSpawner's reconcile) defers to end-of-frame
            Assert.AreEqual(0, Object.FindObjectsByType<PickupController>()
                .Count(p => p.ItemId == "clan_journal"), "pickups despawned once collected");
            Debug.Log("[P2Test] q_journals completed via 3 real world pickups");
        }

        [UnityTest]
        public IEnumerator Explore_ProximityCompletesStep_ThenCollectFinishesQLostSeed()
        {
            _engine.StartQuest("q_lost_seed");   // explore crystal_cave -> collect rare_seed x1
            UI.DialogueUI.Close();
            yield return null;

            Assert.IsTrue(Object.FindObjectsByType<ExploreMarkerController>().Length > 0,
                "explore beacon spawned");
            Assert.IsTrue(ExploreTargets.TryGetPosition("crystal_cave", out var target));
            _player.transform.position = target;

            float deadline = Time.time + 3f;
            while (_engine.StepIndex("q_lost_seed") == 0 && Time.time < deadline) yield return null;
            Assert.AreEqual(1, _engine.StepIndex("q_lost_seed"), "proximity poll advanced past explore");
            Assert.AreEqual(0, Object.FindObjectsByType<ExploreMarkerController>().Length,
                "beacon despawned once its step is done");

            var seedPickup = Object.FindObjectsByType<PickupController>()
                .First(p => p.ItemId == "rare_seed");
            seedPickup.Interact(_player);
            UI.DialogueUI.Close();

            Assert.IsTrue(_engine.IsCompleted("q_lost_seed"), "collect rare_seed completes q_lost_seed");
            Debug.Log("[P2Test] q_lost_seed completed via real explore proximity + pickup");
        }

        [UnityTest]
        public IEnumerator MeteorShower_CompletesWaitForStep_QMeteorMystery()
        {
            _engine.StartQuest("q_meteor_mystery");   // wait_for meteor_shower -> explore crash_site
            UI.DialogueUI.Close();
            yield return null;

            Assert.AreEqual(0, _engine.StepIndex("q_meteor_mystery"));
            GameEvents.MeteorShower();   // simulates MeteorShowerService firing at nightfall
            Assert.AreEqual(1, _engine.StepIndex("q_meteor_mystery"), "wait_for completes on the event");

            Assert.IsTrue(ExploreTargets.TryGetPosition("crash_site", out var target));
            _player.transform.position = target;
            float deadline = Time.time + 3f;
            while (_engine.IsActive("q_meteor_mystery") && Time.time < deadline) yield return null;

            Assert.IsTrue(_engine.IsCompleted("q_meteor_mystery"), "explore crash_site finishes the quest");
            Debug.Log("[P2Test] q_meteor_mystery completed via simulated meteor shower + real explore");
        }

        [Test]
        public void DepositSeeds_CompletesQSeedVault_AndConsumesInventory()
        {
            var arboretum = SpawnRestoredLandmark("celestial_arboretum");
            _state.Save.seeds["starlight_berry"] = 4;
            _state.Save.seeds["clover_cover"] = 3;
            _state.Save.seeds["moon_lily"] = 2;   // 3 kinds, 9 total
            _engine.StartQuest("q_seed_vault");   // deposit_seeds kinds:3 count:9
            UI.DialogueUI.Close();

            arboretum.Interact(_player);

            Assert.IsTrue(_engine.IsCompleted("q_seed_vault"), "9 seeds across 3 kinds completes the vault");
            Assert.AreEqual(0, _state.Save.seeds.Values.Sum(), "all 9 seeds consumed");
            Object.DestroyImmediate(arboretum.gameObject);
            Debug.Log("[P2Test] q_seed_vault completed and consumed the deposited seeds");
        }

        [Test]
        public void DepositSeeds_InsufficientVariety_DoesNotComplete()
        {
            var arboretum = SpawnRestoredLandmark("celestial_arboretum");
            _state.Save.seeds["starlight_berry"] = 20;   // only 1 kind
            _engine.StartQuest("q_seed_vault");
            UI.DialogueUI.Close();

            arboretum.Interact(_player);

            Assert.IsFalse(_engine.IsCompleted("q_seed_vault"), "1 kind can't satisfy kinds:3");
            Assert.AreEqual(20, _state.Save.seeds["starlight_berry"], "nothing consumed on failed deposit");
            Object.DestroyImmediate(arboretum.gameObject);
        }

        [Test]
        public void Deliver_GrantsItemOnActivation_CompletesOnTalkToTarget()
        {
            _engine.StartQuest("q_pebbles_past");   // deliver sealed_crate -> elder_nova
            Assert.Contains("sealed_crate", _state.Save.heldItems, "item granted the moment the step is current");

            GameEvents.TalkedTo("elder_nova");

            Assert.IsTrue(_engine.IsCompleted("q_pebbles_past"), "delivering to the target completes the quest");
            Assert.IsFalse(_state.Save.heldItems.Contains("sealed_crate"), "item consumed on delivery");
            Debug.Log("[P2Test] q_pebbles_past completed via real heldItems deliver flow");
        }

        [Test]
        public void Deliver_WrongTarget_DoesNotComplete()
        {
            _engine.StartQuest("q_pebbles_past");
            GameEvents.TalkedTo("orbit");   // not the delivery target
            Assert.IsFalse(_engine.IsCompleted("q_pebbles_past"));
            Assert.Contains("sealed_crate", _state.Save.heldItems, "item still held");
        }

        [Test]
        public void Craft_StarHoneyConversion_CompletesQLanternLight()
        {
            _engine.StartQuest("q_lantern_light");   // craft festival_lantern x10 (simplified: star_honey input)
            for (int i = 0; i < 10; i++) GameEvents.ItemGranted("star_honey");
            Assert.IsTrue(_engine.IsCompleted("q_lantern_light"), "10 star_honey conversions complete the craft step");
            Debug.Log("[P2Test] q_lantern_light completed via star_honey->festival_lantern simplification");
        }

        [Test]
        public void ToolAutoBuy_PurchasesUpgradeTools_OnRealAction()
        {
            _state.Save.level = 20;
            _state.Save.coins = 10000;

            ToolService.Perform(_farming, _state, "till", 8, 8);
            ToolService.Perform(_farming, _state, "water", 9, 9);
            ToolService.Perform(_farming, _state, "harvest", 10, 10);

            foreach (var id in new[] { "cosmic_hoe_2", "cosmic_hoe_3", "solar_watering_can",
                                       "nebula_mister", "star_scythe", "alien_harvester" })
                Assert.Contains(id, _state.Save.tools, $"{id} auto-bought once level+coins qualify");
            Debug.Log($"[P2Test] tool auto-buy: {_state.Save.tools.Count} tools owned, {_state.Save.coins}c left");
        }

        [UnityTest]
        public IEnumerator ToolAutoBuy_EventuallyCompletesQToolsmith()
        {
            _state.Save.level = 20;
            _state.Save.coins = 10000;
            _state.Save.tools.Add("axe");            // quest reward, cost 0
            _state.Save.tools.Add("fishing_rod");    // quest reward, cost 0
            _engine.StartQuest("q_toolsmith");       // own_tools x10
            UI.DialogueUI.Close();

            ToolService.Perform(_farming, _state, "till", 8, 8);
            ToolService.Perform(_farming, _state, "water", 9, 9);
            ToolService.Perform(_farming, _state, "harvest", 10, 10);

            Assert.AreEqual(10, _state.Save.tools.Count, "all 10 tools now owned");
            float deadline = Time.time + 3f;
            while (_engine.IsActive("q_toolsmith") && Time.time < deadline) yield return null;
            Assert.IsTrue(_engine.IsCompleted("q_toolsmith"), "own_tools(10) poll completes q_toolsmith");
            Debug.Log("[P2Test] q_toolsmith completed after auto-buying all 5 previously-unreachable tools");
        }

        [UnityTest]
        public IEnumerator EternalBloom_ThresholdPoll_CompletesAfter59PriorCompletions()
        {
            for (int i = 0; i < 59; i++) _state.Save.questsCompleted.Add($"fake_quest_{i}");
            _engine.StartQuest("q_eternal_bloom");   // quests_completed x59 — threshold poll, not incremental
            UI.DialogueUI.Close();

            float deadline = Time.time + 3f;
            while (_engine.IsActive("q_eternal_bloom") && Time.time < deadline) yield return null;

            Assert.IsTrue(_engine.IsCompleted("q_eternal_bloom"),
                "already having 59 completions satisfies the threshold immediately (no incremental re-count)");
            Assert.Contains("eternal_bloom_statue", _state.Save.cosmeticsOwned, "reward item granted");
            Debug.Log("[P2Test] q_eternal_bloom completed via threshold poll against prior completions");
        }
    }
}
