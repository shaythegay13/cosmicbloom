// Phase 10 verification — full-coverage save/load in two halves that run in
// SEPARATE Unity processes (batch run A writes, batch run B reads), proving
// state survives a genuine editor restart, not just an in-memory roundtrip.
//   Run A: -testFilter SaveRoundtrip_A_WriteFullState
//   Run B: -testFilter SaveRoundtrip_B_ReadAndVerify
using System.IO;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using NUnit.Framework;
using UnityEngine;

namespace CosmicBloom.Tests
{
    public class SaveRoundtripTests
    {
        static string MarkerPath => Path.Combine(Application.persistentDataPath, "overnight_roundtrip.json");
        static string SavePath => Path.Combine(Application.persistentDataPath, "cosmicbloom_save.json");

        [Test]
        public void SaveRoundtrip_A_WriteFullState()
        {
            GameDatabase.LoadAll();
            var state = new GameState(new SaveSnapshot());
            var farming = new FarmingSystem(state);

            // farm activity in two regions
            state.Save.coins = 777;
            state.Save.seeds["starlight_berry"] = 3;
            farming.Till(7, 7);
            farming.Plant(7, 7, "starlight_berry");
            farming.Water(7, 7);
            state.Save.currentRegion = "sun_garden";
            farming.Till(9, 9);
            state.Save.currentRegion = "starlight_vale";

            // quest + npc + restoration + tools + animals + position/time
            state.Save.activeQuests.Add("q_green_thumb");
            state.Save.questStepIndex["q_green_thumb"] = 0;
            state.Save.questStepProgress["q_green_thumb"] = 4;
            state.Save.questsCompleted.Add("q_arrival");
            state.Save.npcsMet.Add("zyx");
            state.FarmFor("starlight_vale").restoration = 33.5f;
            state.FarmFor("starlight_vale").landmarksRestored.Add("moonlit_pond");
            state.Save.tools.Add("cosmic_hoe_3");
            state.Save.selectedTools["till"] = "cosmic_hoe_3";
            state.Save.animals.Add("chicken");
            state.Save.playerX = 12.25f;
            state.Save.playerY = 8.75f;
            state.Save.timeOfDay = 0.42f;
            state.Save.dayNumber = 3;
            state.MarkDirty();

            File.WriteAllText(SavePath, state.Save.ToJson());
            File.WriteAllText(MarkerPath, state.Save.saveVersion.ToString());
            Debug.Log($"[P10Test-A] wrote full state to {SavePath} (saveVersion={state.Save.saveVersion})");
        }

        [Test]
        public void SaveRoundtrip_B_ReadAndVerify()
        {
            Assert.IsTrue(File.Exists(MarkerPath), "run A must have executed in a previous process");
            Assert.IsTrue(File.Exists(SavePath), "save file exists");

            var restored = SaveSnapshot.FromJson(File.ReadAllText(SavePath));

            Assert.AreEqual(777, restored.coins, "coins");
            Assert.AreEqual("starlight_vale", restored.currentRegion, "current region");

            var vale = restored.farms["starlight_vale"];
            var plot = vale.plots["7,7"];
            Assert.IsTrue(plot.tilled, "vale plot tilled");
            Assert.AreEqual("starlight_berry", plot.crop.id, "crop id");
            Assert.IsFalse(plot.crop.needsWater, "was watered");
            Assert.AreEqual(33.5f, vale.restoration, 0.001f, "restoration");
            Assert.Contains("moonlit_pond", vale.landmarksRestored, "landmark");
            Assert.IsTrue(restored.farms["sun_garden"].plots["9,9"].tilled, "sun plot tilled");

            Assert.Contains("q_green_thumb", restored.activeQuests, "active quest");
            Assert.AreEqual(4, restored.questStepProgress["q_green_thumb"], "step progress");
            Assert.Contains("q_arrival", restored.questsCompleted, "completed quest");
            Assert.Contains("zyx", restored.npcsMet, "npc met");
            Assert.Contains("cosmic_hoe_3", restored.tools, "tool owned");
            Assert.AreEqual("cosmic_hoe_3", restored.selectedTools["till"], "tool selection");
            Assert.Contains("chicken", restored.animals, "animal owned");
            Assert.AreEqual(12.25f, restored.playerX, 0.001f, "player x");
            Assert.AreEqual(8.75f, restored.playerY, 0.001f, "player y");
            Assert.AreEqual(0.42f, restored.timeOfDay, 0.001f, "time of day");
            Assert.AreEqual(3, restored.dayNumber, "day number");

            File.Delete(MarkerPath);
            Debug.Log("[P10Test-B] every field verified after cross-process reload ✓");
        }
    }
}
