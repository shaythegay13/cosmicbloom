// Phase 6 verification — tool tiers change farming behavior (data-driven
// areas), tool selection persists in the save, animals spawn from data and
// produce is collectable; screenshot captured with an animal in scene.
using System.Collections;
using System.IO;
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
    public class ToolAnimalTests
    {
        static GameState NewState(int coins = 1000)
        {
            GameDatabase.LoadAll();
            var s = new GameState(new SaveSnapshot());
            s.Save.coins = coins;
            return s;
        }

        [Test]
        public void TillArea_Tier1VersusTier3()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);

            // tier 1 (cosmic_hoe, default loadout): one plot per tap
            int done = ToolService.Perform(farming, state, "till", 8, 8);
            Assert.AreEqual(1, done, "tier-1 hoe tills exactly the tapped cell");
            Assert.IsNull(farming.PlotAt(7, 8), "neighbor untouched");

            // tier 3 (cosmic_hoe_3, "3x1 row"): three cells in one tap
            state.Save.tools.Add("cosmic_hoe_3");
            ToolService.SelectTool(state, "cosmic_hoe_3");
            done = ToolService.Perform(farming, state, "till", 10, 10);
            Assert.AreEqual(3, done, "tier-3 hoe tills a 3x1 row");
            Assert.IsNotNull(farming.PlotAt(9, 10));
            Assert.IsNotNull(farming.PlotAt(11, 10));
            Debug.Log($"[P6Test] till: tier1=1 cell, tier3=3 cells — different behavior confirmed");
        }

        [Test]
        public void WaterArea_Tier3Waters3x3()
        {
            var state = NewState();
            var farming = new FarmingSystem(state);
            state.Save.tools.Add("nebula_mister");

            // plant a 3x3 block of thirsty crops
            state.Save.seeds["starlight_berry"] = 9;
            for (int y = 9; y <= 11; y++)
                for (int x = 9; x <= 11; x++)
                { farming.Till(x, y); farming.Plant(x, y, "starlight_berry"); }

            ToolService.SelectTool(state, "nebula_mister");
            int watered = ToolService.Perform(farming, state, "water", 10, 10);
            Assert.AreEqual(9, watered, "tier-3 mister waters the whole 3x3");
            Assert.IsFalse(farming.PlotAt(9, 9).crop.needsWater);
            Assert.IsFalse(farming.PlotAt(11, 11).crop.needsWater);
        }

        [Test]
        public void ToolSelection_PersistsInSnapshot()
        {
            var state = NewState();
            state.Save.tools.Add("cosmic_hoe_3");
            ToolService.SelectTool(state, "cosmic_hoe_3");
            var restored = SaveSnapshot.FromJson(state.Save.ToJson());
            Assert.AreEqual("cosmic_hoe_3", restored.selectedTools["till"]);
        }

        [UnityTest]
        public IEnumerator Animals_SpawnBuyProduceCollect()
        {
            GameEvents.Clear();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            regions.RegisterRegion("starlight_vale", new Bounds(new Vector3(10, 10), new Vector3(20, 20)));
            var state = NewState(coins: 1000);
            state.Save.level = 10;   // clears unlockLevel gates for early animals

            string granted = null;
            GameEvents.OnItemGranted += item => granted = item;
            string bought = null;
            GameEvents.OnAnimalBought += id => bought = id;

            Assert.IsTrue(AnimalSpawner.TryBuy(state, regions, "chicken"), "buy star chicken");
            Assert.AreEqual("chicken", bought);
            Assert.Contains("chicken", state.Save.animals);
            Assert.AreEqual(1000 - 200, state.Save.coins, "cost from animals.json");
            Assert.AreEqual(1, AnimalSpawner.Spawned.Count);

            var chicken = AnimalSpawner.Spawned[0];
            yield return null;

            // force produce ready via reflection-free path: wait? no — produce timer is 30 min.
            // Interact before ready = pet
            var player = PlayerController.Spawn(chicken.transform.position + new Vector3(0.5f, 0));
            chicken.Interact(player);
            Assert.IsNull(granted, "no produce yet — petting only");
            UI.DialogueUI.Close();

            chicken.ForceProduceReady();   // test hook
            int coinsBefore = state.Save.coins;
            chicken.Interact(player);
            Assert.AreEqual("glowing_egg", granted, "produce item granted for quests");
            Assert.Greater(state.Save.coins, coinsBefore, "produce sale coins");
            UI.DialogueUI.Close();
            Debug.Log($"[P6Test] chicken produce collected: +{state.Save.coins - coinsBefore}c, item={granted}");

            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(chicken.gameObject);
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator CaptureAnimalInSceneScreenshot()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = NewState();
            state.Save.level = 10;
            AnimalSpawner.TryBuy(state, regions, "chicken");
            AnimalSpawner.TryBuy(state, regions, "sheep");
            yield return null;

            var camGo = new GameObject("CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(10f, 8f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            var rt = new RenderTexture(800, 800, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", "phase6_animals.png"), tex.EncodeToPNG());
            Debug.Log("[P6Test] animal screenshot written to Logs/phase6_animals.png");

            Object.DestroyImmediate(camGo);
            foreach (var a in AnimalSpawner.Spawned.Where(a => a != null)) Object.DestroyImmediate(a.gameObject);
            foreach (var kv in grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(regions.gameObject);
        }
    }
}
