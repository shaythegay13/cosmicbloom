// Phase 5 verification — the data-driven quest engine runs the start of the
// real 47-quest main chain end-to-end: q_arrival (cutscene + move_to) →
// q_first_sprout (clear 3 / plant 5 / harvest 1, real farming triggers) →
// q_soil_secrets activation. Rewards (coins/xp/items) asserted from data.
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
    public class QuestEngineTests
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
            _regions.RegisterRegion("starlight_vale", new Bounds(new Vector3(10, 10), new Vector3(20, 20)));
            _player = PlayerController.Spawn(new Vector3(2, 2));   // far from center
            _engine = new GameObject("TestQuestEngine").AddComponent<QuestEngine>();
            _engine.Bind(_state);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_engine.gameObject);
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator MainChain_FirstThreeQuests_CompleteViaRealTriggers()
        {
            // ── q_arrival auto-started; step 0 is a cutscene shown in the dialogue box
            Assert.IsTrue(_engine.IsActive("q_arrival"), "q_arrival active on fresh save");
            Assert.IsTrue(UI.DialogueUI.IsOpen, "opening cutscene presented");
            UI.DialogueUI.Close();                       // player taps through
            Assert.AreEqual(1, _engine.StepIndex("q_arrival"), "cutscene step done");

            // step 1: move_to starting_plot — walk to the vale center, poll ticks at 1 Hz
            _player.transform.position = new Vector3(10, 10);
            float deadline = Time.time + 3f;
            while (_engine.IsActive("q_arrival") && Time.time < deadline) yield return null;

            Assert.IsTrue(_engine.IsCompleted("q_arrival"), "q_arrival completed by arriving");
            Assert.Contains("cosmic_hoe", _state.Save.tools, "reward tool granted");
            Assert.Contains("watering_can", _state.Save.tools, "reward tool granted");
            Debug.Log($"[P5Test] q_arrival done: coins={_state.Save.coins} xp={_state.Save.xp} tools=[{string.Join(",", _state.Save.tools)}]");

            // ── q_first_sprout: clear 3 plots, plant 5 starlight berries, harvest 1
            Assert.IsTrue(_engine.IsActive("q_first_sprout"), "chain advanced to q_first_sprout");
            UI.DialogueUI.Close();   // dismiss any giver dialogue

            _state.Save.seeds["starlight_berry"] = 5;
            for (int i = 0; i < 3; i++) Assert.IsTrue(_farming.Till(6 + i, 6), $"till {i}");
            Assert.AreEqual(1, _engine.StepIndex("q_first_sprout"), "clear_plots(3) step done");

            _farming.Till(9, 6); _farming.Till(10, 6);   // two more beds for the 5 plants
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(_farming.Plant(6 + i, 6, "starlight_berry"), $"plant {i}");
            Assert.AreEqual(2, _engine.StepIndex("q_first_sprout"), "plant_crop(5) step done");

            // grow one berry to maturity and harvest it
            var def = GameDatabase.Crop("starlight_berry");
            _farming.Water(6, 6);
            var plot = _farming.PlotAt(6, 6);
            for (int guard = 0; plot.crop != null && plot.crop.stage < def.stages - 1 && guard < 10; guard++)
            {
                plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - def.GrowMsPerStage - 1;
                _farming.FastForward();
                if (plot.crop.needsWater) _farming.Water(6, 6);
            }
            int coinsBefore = _state.Save.coins;
            Assert.IsTrue(_farming.Harvest(6, 6), "harvest the first sprout");

            Assert.IsTrue(_engine.IsCompleted("q_first_sprout"), "q_first_sprout completed");
            Assert.Contains("axe", _state.Save.tools, "axe reward from data");
            Assert.AreEqual(coinsBefore + def.sellPrice + 50, _state.Save.coins,
                "sale + quest reward coins (50 from quests.json)");

            // ── chain advanced to q_soil_secrets
            Assert.IsTrue(_engine.IsActive("q_soil_secrets"), "q_soil_secrets active");
            Assert.AreEqual("q_soil_secrets", _state.Save.activeQuest, "legacy activeQuest pointer follows chain");
            UI.DialogueUI.Close();

            // ── q_soil_secrets: plant 2 clover_cover, then 2 plots with soil ≥ 60
            _state.Save.seeds["clover_cover"] = 2;
            _farming.Till(12, 6); _farming.Till(13, 6);
            Assert.IsTrue(_farming.Plant(12, 6, "clover_cover"), "clover 1");
            Assert.IsTrue(_farming.Plant(13, 6, "clover_cover"), "clover 2");
            Assert.AreEqual(1, _engine.StepIndex("q_soil_secrets"), "plant_crop(clover x2) step done");

            // fresh plots sit at soil 100 — the ≥60 threshold poll should complete the quest
            float soilDeadline = Time.time + 3f;
            while (_engine.IsActive("q_soil_secrets") && Time.time < soilDeadline) yield return null;
            Assert.IsTrue(_engine.IsCompleted("q_soil_secrets"), "q_soil_secrets completed (3rd sequential completion)");
            Assert.IsTrue(_engine.IsActive("q_green_thumb"), "chain advanced to quest #4");
            Debug.Log($"[P5Test] chain state: completed=[{string.Join(",", _state.Save.questsCompleted)}] active=[{string.Join(",", _state.Save.activeQuests)}]");
        }

        [Test]
        public void QuestProgress_SurvivesSnapshotRoundtrip()
        {
            _state.Save.questStepProgress["q_green_thumb"] = 7;
            _state.Save.activeQuests.Add("q_green_thumb");
            _state.Save.questStepIndex["q_green_thumb"] = 0;

            var json = _state.Save.ToJson();
            var restored = SaveSnapshot.FromJson(json);

            Assert.AreEqual(7, restored.questStepProgress["q_green_thumb"]);
            Assert.Contains("q_green_thumb", restored.activeQuests);
            Assert.AreEqual(2, restored.schema, "schema stays 2");
        }

        void GrowToHarvestable(int x, int y, string cropId)
        {
            var def = GameDatabase.Crop(cropId);
            var plot = _farming.PlotAt(x, y);
            _farming.Water(x, y);
            for (int guard = 0; plot.crop != null && plot.crop.stage < def.stages - 1 && guard < 12; guard++)
            {
                plot.crop.stageStart = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - def.GrowMsPerStage - 1;
                _farming.FastForward();
                if (plot.crop != null && plot.crop.needsWater) _farming.Water(x, y);
            }
        }

        // Phase 7 finding: BumpDistinct's shared distinct-set counter required
        // step.count DISTINCT kinds for sell_distinct, not step.count total sales
        // across step.kinds varieties — q_trade_routes' count:10 exceeds every
        // region's crop variety (max 6 in starlight_vale), making it uncompletable
        // as written. Fixed with a dedicated total+kinds tally (BumpSellDistinct),
        // mirroring deposit_seeds' own kinds+count split.
        [Test]
        public void SellDistinct_TotalCountAcrossMinimumKinds_CompletesQTradeRoutes()
        {
            _engine.StartQuest("q_trade_routes");   // sell_distinct kinds:3 count:10
            UI.DialogueUI.Close();

            _state.Save.seeds["starlight_berry"] = 20;
            for (int i = 0; i < 6; i++)
            {
                if (_farming.PlotAt(20, 20) == null) _farming.Till(20, 20);
                Assert.IsTrue(_farming.Plant(20, 20, "starlight_berry"));
                GrowToHarvestable(20, 20, "starlight_berry");
                Assert.IsTrue(_farming.Harvest(20, 20));
            }
            Assert.AreEqual(6, _engine.StepProgress("q_trade_routes"), "6 total sales tracked");
            Assert.IsFalse(_engine.IsCompleted("q_trade_routes"), "one kind only: count ok, kinds not");

            _state.Save.seeds["potato"] = 1;
            _farming.Till(21, 20);
            Assert.IsTrue(_farming.Plant(21, 20, "potato"));
            GrowToHarvestable(21, 20, "potato");
            Assert.IsTrue(_farming.Harvest(21, 20));

            _state.Save.seeds["clover_cover"] = 1;
            _farming.Till(22, 20);
            Assert.IsTrue(_farming.Plant(22, 20, "clover_cover"));
            GrowToHarvestable(22, 20, "clover_cover");
            Assert.IsTrue(_farming.Harvest(22, 20));

            Assert.AreEqual(8, _engine.StepProgress("q_trade_routes"), "8 total across 3 kinds");
            Assert.IsFalse(_engine.IsCompleted("q_trade_routes"), "8 of 10 total is not enough yet");

            for (int i = 0; i < 2; i++)
            {
                _state.Save.seeds["starlight_berry"] = 1;
                if (_farming.PlotAt(23 + i, 20) == null) _farming.Till(23 + i, 20);
                Assert.IsTrue(_farming.Plant(23 + i, 20, "starlight_berry"));
                GrowToHarvestable(23 + i, 20, "starlight_berry");
                Assert.IsTrue(_farming.Harvest(23 + i, 20));
            }

            Assert.IsTrue(_engine.IsCompleted("q_trade_routes"), "10 sales across 3 kinds completes q_trade_routes");
            Debug.Log("[P7Test] q_trade_routes completed via 10 sales across 3 distinct crop kinds");
        }

        [UnityTest]
        public IEnumerator SideQuest_ActivatesOnGiverChat()
        {
            UI.DialogueUI.Close();
            // find a quest no other quest unlocks (side content) whose giver exists
            QuestDef side = null;
            foreach (var q in GameDatabase.Quests)
            {
                bool inChain = false;
                foreach (var other in GameDatabase.Quests)
                    if (other.unlocks == q.id) { inChain = true; break; }
                if (!inChain && q.id != "q_arrival" && q.giver != null) { side = q; break; }
            }
            Assert.IsNotNull(side, "data has side quests");

            GameEvents.TalkedTo(side.giver);
            yield return null;
            Assert.IsTrue(_engine.IsActive(side.id) || _engine.IsCompleted(side.id),
                $"side quest {side.id} activated by talking to {side.giver}");
            Debug.Log($"[P5Test] side quest {side.id} activated via {side.giver}");
        }
    }
}
