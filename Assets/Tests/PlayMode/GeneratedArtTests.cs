// Phase 5 verification — real generated pixel art (Tools/art/build-cosmic.mjs
// output, imported under Assets/Resources/Sprites/Generated/) actually renders
// in-game: a farm plot shows a real crop sprite at its final growth stage, and
// a spawned NPC shows its own generated portrait — not the tinted-square /
// capsule+accent placeholders. Also captures a screenshot of both for visual
// review (Logs/phase5_sprites.png).
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
    public class GeneratedArtTests
    {
        RegionManager _regions;
        System.Collections.Generic.Dictionary<string, Grid> _grids;
        System.Collections.Generic.List<NpcController> _npcs;
        GameState _state;
        FarmingSystem _farming;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            GeneratedSprites.ClearCache();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _npcs = NpcSpawner.SpawnAll(_regions);

            _state = new GameState(new SaveSnapshot());
            _state.Save.currentRegion = "starlight_vale";
            _farming = new FarmingSystem(_state);
            FarmGrid.Create("starlight_vale", _grids["starlight_vale"], _farming, _state);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var n in _npcs.Where(n => n != null)) Object.DestroyImmediate(n.gameObject);
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator FarmPlotAndNpc_ShowGeneratedSprites_Screenshot()
        {
            // Force a starlight_vale-native crop to its final (ready) stage at a
            // known farmable plot so the screenshot captures real, non-sprout art.
            var crop = GameDatabase.Crops.First(c => c.region == "starlight_vale");
            _state.Save.seeds[crop.id] = 1;
            _farming.Till(16, 16);
            Assert.IsTrue(_farming.Plant(16, 16, crop.id), $"plant {crop.id} at (16,16)");
            var plot = _farming.PlotAt(16, 16);
            plot.crop.stage = crop.stages - 1;
            plot.crop.needsWater = false;
            yield return null;   // FarmPlot.LateUpdate refreshes visuals from live state

            var plotGo = GameObject.Find("Plot_16_16");
            Assert.IsNotNull(plotGo, "plot view exists");
            var plotSr = plotGo.GetComponent<SpriteRenderer>();
            var expectedCropSprite = GeneratedSprites.Crop(crop.id, plot.crop.stage);
            Assert.IsNotNull(expectedCropSprite, $"generated sprite exists for {crop.id} stage {plot.crop.stage}");
            Assert.AreSame(expectedCropSprite, plotSr.sprite,
                "plot renders the real generated crop sprite, not the placeholder square");
            Debug.Log($"[P5Test] plot (16,16) shows generated sprite for {crop.id}@{plot.crop.stage}");

            var npc = _npcs.First(n => n.Def.region == "starlight_vale");
            var npcBody = npc.GetComponent<SpriteRenderer>();
            var expectedNpcSprite = GeneratedSprites.Npc(npc.Def.id);
            Assert.IsNotNull(expectedNpcSprite, $"generated sprite exists for npc {npc.Def.id}");
            Assert.AreSame(expectedNpcSprite, npcBody.sprite,
                "npc renders the real generated portrait, not the placeholder capsule");
            Assert.IsNull(npc.transform.Find("Accent"), "generated-art NPCs skip the placeholder accent chip");
            Debug.Log($"[P5Test] npc {npc.Def.id} shows generated sprite");

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 16f;   // whole 40x40 vale: plot at (16.5,16.5) + NPC ring r=14.4
            cam.transform.position = new Vector3(20f, 20f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            yield return null;
            yield return null;   // WaitForEndOfFrame never completes in batchmode — use frame yields

            var rt = new RenderTexture(1000, 1000, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", "phase5_sprites.png"), tex.EncodeToPNG());
            Debug.Log("[P5Test] captured Logs/phase5_sprites.png");

            Object.DestroyImmediate(camGo);
        }
    }
}
