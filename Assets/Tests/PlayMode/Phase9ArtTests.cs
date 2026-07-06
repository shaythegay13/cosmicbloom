// Phase 9 verification — the art pipeline extended to animals and ground
// tiles (Tools/art/build-cosmic.mjs), same reuse pattern Phase 5 proved for
// crops/NPCs. Three things to confirm:
//  1. All 15 animals render their own generated sprite, not the tinted
//     Critter blob — with a specific check that the chicken (flagged in the
//     phase spec as reading as an undifferentiated grey blob) now carries a
//     distinct sprite from the other two birds (comet_duck, star_owl).
//  2. Ground tiles render the new texture-mask sprites while still tinted
//     to each region's exact groundTiers hex — the mask/tint split means
//     regions.json stays the single source of truth for color, so this also
//     re-asserts the same color check RegionWorldTests already makes.
//  3. Several crop archetypes (stalk/bush/root/flower, all native to
//     Starlight Vale) reach final growth stage side by side, to confirm
//     Phase 5's crop art actually differentiates at full maturity, not just
//     that each one individually has generated art.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace CosmicBloom.Tests
{
    public class Phase9ArtTests
    {
        static void Screenshot(Camera cam, int w, int h, string fileName)
        {
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", fileName), tex.EncodeToPNG());
            Debug.Log($"[P9Test] captured Logs/{fileName}");
        }

        [UnityTest]
        public IEnumerator AllAnimals_RenderGeneratedSprites_ChickenDistinctFromOtherBirds()
        {
            GameDatabase.LoadAll();
            var state = new GameState(new SaveSnapshot());
            var spawned = new List<AnimalController>();

            int i = 0;
            foreach (var def in GameDatabase.Animals)
            {
                var pos = new Vector3((i % 5) * 3f, -(i / 5) * 3f, 0f);
                spawned.Add(AnimalController.Spawn(def, pos, state));
                i++;
            }
            yield return null;

            var byId = new Dictionary<string, Sprite>();
            foreach (var a in spawned)
            {
                var expected = GeneratedSprites.Animal(a.Def.id);
                Assert.IsNotNull(expected, $"generated sprite exists for animal {a.Def.id}");
                var sr = a.GetComponent<SpriteRenderer>();
                Assert.AreSame(expected, sr.sprite, $"{a.Def.id} renders the real generated sprite, not the placeholder critter");
                Assert.AreEqual(Color.white, sr.color, $"{a.Def.id} generated sprite is unlerped (color carried by the PNG, not a tint)");
                byId[a.Def.id] = sr.sprite;
            }

            // The phase's specific ask: chicken must read as a chicken, not an
            // undifferentiated grey blob shared with the other two birds.
            Assert.AreNotEqual(byId["chicken"], byId["comet_duck"], "chicken sprite distinct from comet_duck");
            Assert.AreNotEqual(byId["chicken"], byId["star_owl"], "chicken sprite distinct from star_owl");
            Debug.Log($"[P9Test] {spawned.Count} animals all render distinct generated sprites; chicken confirmed distinct from other birds");

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.transform.position = new Vector3(6f, -3f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            yield return null;
            yield return null;   // WaitForEndOfFrame never completes in batchmode — use frame yields

            Screenshot(cam, 1200, 800, "phase9_animals.png");

            Object.DestroyImmediate(camGo);
            foreach (var a in spawned) if (a != null) Object.DestroyImmediate(a.gameObject);
        }

        [UnityTest]
        public IEnumerator AllRegionTiles_UseGeneratedMasks_StillTintedToRegionsJsonColor()
        {
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            yield return null;

            foreach (var def in GameDatabase.Regions)
            {
                var tilemap = grids[def.id].GetComponentInChildren<Tilemap>();
                AssertTile(tilemap, def, 0, 0, 0);     // outer ring
                AssertTile(tilemap, def, 4, 4, 1);     // mid ring
                AssertTile(tilemap, def, 10, 10, 2);   // lush core
                Debug.Log($"[P9Test] {def.id}: generated tile masks in place, tint still matches groundTiers");
            }

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(90f, 10f, -10f);
            cam.orthographicSize = 14f;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            yield return null;
            yield return null;

            Screenshot(cam, 2000, 280, "phase9_tiles.png");

            Object.DestroyImmediate(camGo);
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(regions.gameObject);
        }

        static void AssertTile(Tilemap tilemap, RegionDef def, int x, int y, int tier)
        {
            var tile = tilemap.GetTile<Tile>(new Vector3Int(x, y, 0));
            Assert.IsNotNull(tile, $"{def.id} tile at {x},{y}");
            var expectedSprite = GeneratedSprites.Tile(def.id, tier);
            Assert.IsNotNull(expectedSprite, $"generated tile mask exists for {def.id} tier {tier}");
            Assert.AreSame(expectedSprite, tile.sprite, $"{def.id} tier {tier} renders the generated texture mask, not the flat placeholder square");
            var expectedColor = ProceduralSprites.ParseHex(def.groundTiers[tier]);
            Assert.AreEqual(expectedColor, tile.color, $"{def.id} tier {tier} tint still matches groundTiers — mask/tint split preserves regions.json as the color source of truth");
        }

        [UnityTest]
        public IEnumerator MatureCrops_DifferentiateAcrossArchetypes()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.currentRegion = "starlight_vale";
            var farming = new FarmingSystem(state);
            FarmGrid.Create("starlight_vale", grids["starlight_vale"], farming, state);

            // Four vale-native crops covering four distinct archetypes
            // (stalk/bush/root/flower) — the fifth archetype (vine) has no
            // starlight_vale-native crop, so isn't included here; it already
            // gets its own single-crop coverage via Phase 5's GeneratedArtTests.
            var plants = new (string id, int x, int y)[]
            {
                ("wheat", 9, 9),          // stalk
                ("star_tomato", 11, 9),   // bush
                ("carrot", 9, 11),        // root
                ("clover_cover", 11, 11), // flower
            };

            var sprites = new List<Sprite>();
            foreach (var (id, x, y) in plants)
            {
                var def = GameDatabase.Crop(id);
                state.Save.seeds[id] = 1;
                Assert.IsTrue(farming.Till(x, y), $"till {x},{y}");
                Assert.IsTrue(farming.Plant(x, y, id), $"plant {id} at ({x},{y})");
                var plot = farming.PlotAt(x, y);
                plot.crop.stage = def.stages - 1;
                plot.crop.needsWater = false;
            }
            yield return null;   // FarmPlot.LateUpdate refreshes visuals from live state

            foreach (var (id, x, y) in plants)
            {
                var def = GameDatabase.Crop(id);
                var plotGo = GameObject.Find($"Plot_{x}_{y}");
                Assert.IsNotNull(plotGo, $"plot view exists at {x},{y}");
                var sr = plotGo.GetComponent<SpriteRenderer>();
                var expected = GeneratedSprites.Crop(id, def.stages - 1);
                Assert.IsNotNull(expected, $"generated sprite exists for {id} at final stage");
                Assert.AreSame(expected, sr.sprite, $"{id} ({def.archetype}) renders its real mature-stage generated sprite");
                sprites.Add(sr.sprite);
            }
            Assert.AreEqual(sprites.Distinct().Count(), sprites.Count, "all four mature-stage archetypes render visually distinct sprites");
            Debug.Log($"[P9Test] {plants.Length} mature crops (stalk/bush/root/flower) confirmed distinct at final growth stage");

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4f;
            cam.transform.position = new Vector3(10f, 10f, -10f);
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            yield return null;
            yield return null;

            Screenshot(cam, 900, 900, "phase9_crops_mature.png");

            Object.DestroyImmediate(camGo);
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }

        [UnityTest]
        public IEnumerator AllLandmarks_RenderGeneratedSprites_RuinedVersusRestoredDistinct()
        {
            GameEvents.Clear();
            var regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            var grids = RegionWorldBuilder.BuildAll(regions);
            var state = new GameState(new SaveSnapshot());
            state.Save.coins = 20000;   // covers even heartseed_roots' 8000 restoreCost
            var landmarks = LandmarkSpawner.SpawnAll(regions, state);
            yield return null;

            // Every landmark starts ruined — confirm its own generated ruined sprite.
            var ruinedById = new Dictionary<string, Sprite>();
            foreach (var lm in landmarks)
            {
                var expected = GeneratedSprites.Landmark(lm.Def.id, false);
                Assert.IsNotNull(expected, $"generated ruined sprite exists for landmark {lm.Def.id}");
                var sr = lm.GetComponent<SpriteRenderer>();
                Assert.AreSame(expected, sr.sprite, $"{lm.Def.id} renders the real generated ruined sprite, not the flat placeholder");
                Assert.AreEqual(Color.white, sr.color, $"{lm.Def.id} generated sprite is unlerped");
                ruinedById[lm.Def.id] = sr.sprite;
            }

            // Restore every other landmark so the screenshot shows both states, and
            // confirm each swaps to its own distinct restored sprite (not a re-tint).
            var player = PlayerController.Spawn(Vector3.zero);
            for (int i = 0; i < landmarks.Count; i += 2)
            {
                var lm = landmarks[i];
                lm.Interact(player);
                var sr = lm.GetComponent<SpriteRenderer>();
                var expectedRestored = GeneratedSprites.Landmark(lm.Def.id, true);
                Assert.IsNotNull(expectedRestored, $"generated restored sprite exists for landmark {lm.Def.id}");
                Assert.AreSame(expectedRestored, sr.sprite, $"{lm.Def.id} swaps to its real generated restored sprite");
                Assert.AreNotEqual(ruinedById[lm.Def.id], sr.sprite, $"{lm.Def.id} restored sprite differs from its ruined sprite");
            }
            Debug.Log($"[P9Test] {landmarks.Count} landmarks render generated art; alternating landmarks restored to confirm the ruined/restored swap");

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.transform.position = new Vector3(90f, 10f, -10f);
            cam.orthographicSize = 14f;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            yield return null;
            yield return null;

            Screenshot(cam, 2000, 280, "phase9_landmarks.png");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(player.gameObject);
            foreach (var lm in landmarks) if (lm != null) Object.DestroyImmediate(lm.gameObject);
            foreach (var def in GameDatabase.Regions)
            {
                var go = GameObject.Find(RegionWorldBuilder.ToPascal(def.id));
                if (go != null) Object.DestroyImmediate(go);
            }
            Object.DestroyImmediate(regions.gameObject);
            GameEvents.Clear();
        }
    }
}
