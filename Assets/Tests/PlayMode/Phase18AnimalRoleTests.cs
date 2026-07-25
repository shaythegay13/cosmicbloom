// P3-Phase 18 verification — the livestock/companion role split.
//
// The two headline behaviors the phase promises are proved against real
// transforms and real save state, not assumed:
//   * LIVESTOCK: bought, then PLACED on a cell nowhere near its default paddock
//     spot, and still producing correctly there (species clock, coins, the
//     ItemGranted signal `collect` quest steps consume) — plus the placement
//     surviving a save roundtrip.
//   * COMPANION: actually WALKS to a new NpcSchedule stop when the day period
//     changes, and responds to a tap with a real dialogue line.
//
// The screenshot tests render a camera, so they belong in the RENDER split,
// never the -nographics filter.
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
    public class Phase18AnimalRoleTests
    {
        RegionManager _regions;
        System.Collections.Generic.Dictionary<string, Grid> _grids;
        TimeManager _time;

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            AnimalSpawner.ClearAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _time = new GameObject("TestTime").AddComponent<TimeManager>();
        }

        [TearDown]
        public void TearDown()
        {
            AnimalSpawner.ClearAll();
            if (_time != null) Object.DestroyImmediate(_time.gameObject);
            if (_grids != null) foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            if (_regions != null) Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        static GameState RichState()
        {
            var state = new GameState(new SaveSnapshot());
            state.AddCoins(50000);
            state.Save.level = 30;
            return state;
        }

        /// <summary>First placeable cell of the given ground tier, scanned deterministically.
        /// Tests pick cells this way rather than hardcoding coordinates, so a landmark
        /// footprint or plaza moving onto a chosen cell can't silently break them.</summary>
        static Vector2Int PlaceableCell(GameState state, string region, int tier, int skip = 0)
        {
            for (int y = 0; y < RegionWorldBuilder.Size; y++)
                for (int x = 0; x < RegionWorldBuilder.Size; x++)
                {
                    if (RegionWorldBuilder.TierAt(x, y) != tier) continue;
                    if (!AnimalSpawner.CanPlaceAt(state, region, x, y)) continue;
                    if (skip-- > 0) continue;
                    return new Vector2Int(x, y);
                }
            Assert.Fail($"no placeable tier-{tier} cell in {region}");
            return default;
        }

        // ------------------------------------------------------------- roster split

        [Test]
        public void RoleSplit_MatchesApprovedRoster()
        {
            // User-approved 2026-07-25. Deliberately diverges from animals.json's
            // descriptive `kind` in four places — see the DECISION in AnimalRoles.cs.
            var expectedCompanions = new[]
            {
                "comet_duck", "moon_rabbit", "biolume_fox",
                "cosmic_tortoise", "glow_axolotl", "star_owl", "star_slime",
            };
            var expectedLivestock = new[]
            {
                "chicken", "sheep", "asteroid_pig", "crater_goat", "nebula_cow",
                "bee_swarm", "butterfly_swarm", "firefly_swarm",
            };

            CollectionAssert.AreEquivalent(expectedCompanions, AnimalRoles.Companions.ToArray());
            foreach (var id in expectedCompanions)
                Assert.IsTrue(AnimalRoles.IsCompanion(id), $"{id} is a companion");
            foreach (var id in expectedLivestock)
                Assert.IsTrue(AnimalRoles.IsLivestock(id), $"{id} is livestock");

            // Every animal in the data has exactly one role, and the two buckets
            // together account for the whole roster (8 + 7 = 15).
            Assert.AreEqual(15, GameDatabase.Animals.Count, "roster size unchanged");
            Assert.AreEqual(8, GameDatabase.Animals.Count(a => AnimalRoles.IsLivestock(a.id)));
            Assert.AreEqual(7, GameDatabase.Animals.Count(a => AnimalRoles.IsCompanion(a.id)));
        }

        [Test]
        public void CompanionsKeepDataProduce_SplitIsBehavioralNotEconomic()
        {
            // DECISION (Phase 18): companions are not stripped of income. The two
            // companions whose animals.json entry defines produce keep it verbatim.
            var slime = GameDatabase.Animals.First(a => a.id == "star_slime");
            var duck = GameDatabase.Animals.First(a => a.id == "comet_duck");
            Assert.AreEqual("stardust", slime.produce);
            Assert.AreEqual(120f, slime.produceMinutes);
            Assert.AreEqual("comet_egg", duck.produce);
            Assert.AreEqual(45f, duck.produceMinutes);

            // ...and the quest-critical livestock produce is untouched by the split.
            var chicken = GameDatabase.Animals.First(a => a.id == "chicken");
            var bees = GameDatabase.Animals.First(a => a.id == "bee_swarm");
            Assert.AreEqual("glowing_egg", chicken.produce, "q_egg_basket's collect item");
            Assert.AreEqual("star_honey", bees.produce, "q_lantern_light's crafting input");
            Assert.IsTrue(AnimalRoles.IsLivestock("bee_swarm"), "bee_swarm is placeable livestock");
        }

        // ------------------------------------------------------- livestock placement

        [Test]
        public void Placement_RejectsReservedTilledAndOccupiedCells()
        {
            var state = RichState();
            const string region = "starlight_vale";

            Assert.IsTrue(AnimalSpawner.CanPlaceAt(state, region, 20, 20), "open lush ground is placeable");
            Assert.IsTrue(AnimalSpawner.CanPlaceAt(state, region, 2, 2), "open WILD ground is placeable too");
            Assert.IsFalse(AnimalSpawner.CanPlaceAt(state, region, -1, 20), "out of bounds rejected");
            Assert.IsFalse(AnimalSpawner.CanPlaceAt(state, region, RegionWorldBuilder.Size, 20), "out of bounds rejected");

            // a reserved cell (landmark footprint / social plaza) is never placeable
            var reserved = Enumerable.Range(0, RegionWorldBuilder.Size)
                .SelectMany(x => Enumerable.Range(0, RegionWorldBuilder.Size).Select(y => new Vector2Int(x, y)))
                .First(c => ReservedTiles.IsReserved(c.x, c.y));
            Assert.IsFalse(AnimalSpawner.CanPlaceAt(state, region, reserved.x, reserved.y),
                $"reserved cell ({reserved.x},{reserved.y}) rejected");

            // tilled soil belongs to crops — its FarmPlot collider owns the tap
            var farming = new FarmingSystem(state);
            farming.Till(18, 18);
            Assert.IsFalse(AnimalSpawner.CanPlaceAt(state, region, 18, 18), "tilled plot rejected");

            // one animal per cell
            state.Save.animals.Add("chicken");
            Assert.IsTrue(AnimalSpawner.TryPlace(state, _regions, region, "chicken", 20, 20));
            state.Save.animals.Add("sheep");
            Assert.IsFalse(AnimalSpawner.CanPlaceAt(state, region, 20, 20, "sheep"), "cell already occupied");
            Assert.IsTrue(AnimalSpawner.CanPlaceAt(state, region, 20, 20, "chicken"),
                "the occupant itself may be re-placed on its own cell");
        }

        [Test]
        public void Companions_AreNotPlaceable()
        {
            var state = RichState();
            state.Save.animals.Add("moon_rabbit");
            Assert.IsFalse(AnimalPlacement.Arm("moon_rabbit"), "companions can't arm place mode");
            Assert.IsFalse(AnimalSpawner.TryPlace(state, _regions, "starlight_vale", "moon_rabbit", 20, 20));
            Assert.IsFalse(state.Save.animalPlacements.ContainsKey(
                AnimalSpawner.PlacementKey("starlight_vale", "moon_rabbit")));

            Assert.IsTrue(AnimalPlacement.Arm("chicken"), "livestock can");
            AnimalPlacement.Disarm();
            Assert.IsFalse(AnimalPlacement.IsArmed);
        }

        [Test]
        public void PlacedLivestock_MovesToNewCell_AndStillProducesThere()
        {
            var state = RichState();
            const string region = "starlight_vale";
            Assert.IsTrue(_regions.TryGetBounds(region, out var bounds));

            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "chicken"), "bought the chicken");
            var chicken = AnimalSpawner.Spawned.First(a => a.Def.id == "chicken" && a.RegionId == region);
            var paddockPos = chicken.transform.position;

            // place it on outer WILD ground outside the lush core — somewhere the old
            // fixed 256-cell grid model could never have put an animal
            var cell = PlaceableCell(state, region, tier: 0);
            int px = cell.x, py = cell.y;
            Assert.AreEqual(0, RegionWorldBuilder.TierAt(px, py), "target is outer wild ground");
            Assert.IsTrue(AnimalSpawner.TryPlace(state, _regions, region, "chicken", px, py));

            var expected = RegionWorldBuilder.CellCenterWorld(bounds.center, px, py);
            Assert.Less((chicken.transform.position - expected).magnitude, 0.01f,
                "live instance moved to the placed cell");
            Assert.Greater((chicken.transform.position - paddockPos).magnitude, 10f,
                "placed cell is genuinely somewhere new, not the default paddock");
            Assert.AreEqual($"{px},{py}",
                state.Save.animalPlacements[AnimalSpawner.PlacementKey(region, "chicken")],
                "placement persisted to the save");

            // ...and it produces correctly from its new home
            string granted = null;
            GameEvents.OnItemGranted += id => granted = id;
            int before = state.Save.coins;
            chicken.ForceProduceReady();
            Assert.IsTrue(chicken.ProduceReady, "produce came ready at the new location");
            chicken.Interact(null);

            var def = GameDatabase.Animals.First(a => a.id == "chicken");
            Assert.AreEqual("glowing_egg", granted, "ItemGranted fired for the collect quest step");
            Assert.AreEqual(before + Mathf.Max(2, def.cost / 40), state.Save.coins, "coins granted");
            Assert.IsFalse(chicken.ProduceReady, "species clock rescheduled after collecting");
            Debug.Log($"[P18Test] chicken placed at wild ({px},{py}) {chicken.transform.position} " +
                      $"and produced glowing_egg (+{Mathf.Max(2, def.cost / 40)}c) ✓");
        }

        [Test]
        public void Placement_SurvivesSaveRoundtripAndRespawn()
        {
            var state = RichState();
            const string region = "sun_garden";
            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "sheep"), "bought the sheep");
            var cell = PlaceableCell(state, region, tier: 1);
            Assert.IsTrue(AnimalSpawner.TryPlace(state, _regions, region, "sheep", cell.x, cell.y),
                $"placed the sheep at {cell}");

            // roundtrip through the real serializer
            var restored = new GameState(SaveSnapshot.FromJson(state.Save.ToJson()));
            Assert.AreEqual($"{cell.x},{cell.y}",
                restored.Save.animalPlacements[AnimalSpawner.PlacementKey(region, "sheep")]);

            // respawning from the restored save puts the animal back on its cell
            AnimalSpawner.SpawnOwned(restored, _regions);
            Assert.IsTrue(_regions.TryGetBounds(region, out var bounds));
            var sheep = AnimalSpawner.Spawned.First(a => a.Def.id == "sheep" && a.RegionId == region);
            var expected = RegionWorldBuilder.CellCenterWorld(bounds.center, cell.x, cell.y);
            Assert.Less((sheep.transform.position - expected).magnitude, 0.01f,
                "restored save respawns the sheep on its placed cell");
        }

        [Test]
        public void CellWorldConversion_RoundTrips()
        {
            Assert.IsTrue(_regions.TryGetBounds("moon_garden", out var bounds));
            foreach (var c in new[] { new Vector2Int(0, 0), new Vector2Int(20, 20), new Vector2Int(39, 39) })
            {
                var world = RegionWorldBuilder.CellCenterWorld(bounds.center, c.x, c.y);
                Assert.AreEqual(c, RegionWorldBuilder.WorldToCell(bounds.center, world), $"roundtrip {c}");

                // Must agree with the tilemap Grid the world is actually built from —
                // in the XY plane. Z deliberately differs: Unity's Grid centers a cell
                // in Z as well, while animals (like every other sprite the game spawns)
                // live on the z=0 plane.
                var gridWorld = _grids["moon_garden"].GetCellCenterWorld(new Vector3Int(c.x, c.y, 0));
                Assert.Less(((Vector2)world - (Vector2)gridWorld).magnitude, 0.001f,
                    $"XY agrees with Grid at {c}: mine={world} grid={gridWorld}");
                Assert.AreEqual(bounds.center.z, world.z, 0.0001f, $"cell center stays on the region plane at {c}");
            }
        }

        // -------------------------------------------------------------- companions

        [UnityTest]
        public IEnumerator CompanionWalksToNewStop_AtPeriodTransition()
        {
            var state = RichState();
            const string region = "starlight_vale";
            Assert.IsTrue(_regions.TryGetBounds(region, out var bounds));

            _time.timeOfDay = 0.30f;   // Morning
            Assert.AreEqual(TimeManager.DayPeriod.Morning, _time.CurrentPeriod);

            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "moon_rabbit"));
            var pet = AnimalSpawner.Spawned.First(a => a.Def.id == "moon_rabbit" && a.RegionId == region);
            Assert.IsTrue(pet.IsCompanion);
            pet.walkSpeed = 200f;   // batchmode deltaTime is tiny; keep the walk short in frames
            yield return null;      // Start() subscribes to OnPeriodChanged

            var companions = GameDatabase.Animals.Where(a => AnimalRoles.IsCompanion(a.id)).ToList();
            float angle = CompanionSchedule.RingAngle(companions.FindIndex(a => a.id == "moon_rabbit"), companions.Count);
            var workPos = CompanionSchedule.StopPosition(NpcSchedule.Stop.Work, bounds.center, angle);
            var socialPos = CompanionSchedule.StopPosition(NpcSchedule.Stop.Social, bounds.center, angle);

            Assert.Less((pet.transform.position - workPos).magnitude, 0.05f,
                "morning spawn snaps to the Work stop");
            var morningPos = pet.transform.position;

            _time.timeOfDay = 0.55f;   // Midday → OnPeriodChanged next Update
            for (int i = 0; i < 3000 && (pet.transform.position - socialPos).magnitude > 0.05f; i++)
                yield return null;

            Assert.Greater((pet.transform.position - morningPos).magnitude, 5f,
                "companion transform actually moved away from the morning stop");
            Assert.Less((pet.transform.position - socialPos).magnitude, 0.05f,
                "companion arrived at the Midday Social stop (shared plaza)");
            Debug.Log($"[P18Test] moon_rabbit walked Work {morningPos} → Social {pet.transform.position} ✓");
        }

        [Test]
        public void Companions_ScatterAndNeverShareAnNpcBearing()
        {
            // Phase 16's live-playtest bug: a shared bearing renders a whole roster as
            // one sprite. Companions must scatter against each other AND against NPCs.
            var center = new Vector3(20f, 20f, 0f);
            var companions = GameDatabase.Animals.Where(a => AnimalRoles.IsCompanion(a.id)).ToList();
            var spots = Enumerable.Range(0, companions.Count)
                .Select(i => CompanionSchedule.StopPosition(
                    NpcSchedule.Stop.Social, center, CompanionSchedule.RingAngle(i, companions.Count)))
                .ToList();

            for (int i = 0; i < spots.Count; i++)
                for (int j = i + 1; j < spots.Count; j++)
                    Assert.Greater((spots[i] - spots[j]).magnitude, 1f,
                        $"companions {i} and {j} must not stack");

            // NpcController's bearing formula uses phase +0.4f; companions use RingPhase
            for (int i = 0; i < companions.Count; i++)
                for (int n = 0; n < 10; n++)
                {
                    float npcAngle = (Mathf.PI * 2f * n) / 10f + 0.4f;
                    var npcSpot = NpcSchedule.StopPosition(NpcSchedule.Stop.Social, center, npcAngle);
                    Assert.Greater((spots[i] - npcSpot).magnitude, 0.2f,
                        $"companion {i} must not sit exactly on NPC slot {n}");
                }
        }

        [Test]
        public void CompanionInteraction_ShowsAuthoredLine_NotInventedContent()
        {
            var state = RichState();
            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "cosmic_tortoise"));
            var pet = AnimalSpawner.Spawned.First(a => a.Def.id == "cosmic_tortoise");
            var def = GameDatabase.Animals.First(a => a.id == "cosmic_tortoise");

            // DATA GAP: animals.json has no dialogue object, so the line is the
            // animal's authored `notes` prose — never content invented in code.
            Assert.AreEqual(def.notes, pet.CompanionLine(false), "intro line is the authored notes");
            Assert.AreEqual(def.notes, pet.CompanionLine(true), "idle line too — no distinct data exists yet");

            pet.Interact(null);
            Assert.IsTrue(UI.DialogueUI.IsOpen, "tapping a companion opens dialogue");
            Assert.AreEqual(def.notes, UI.DialogueUI.CurrentLine, "dialogue shows the companion's line");
            Assert.AreEqual(def.name, UI.DialogueUI.CurrentSpeaker);
            Debug.Log($"[P18Test] cosmic_tortoise said: \"{UI.DialogueUI.CurrentLine}\" ✓");
            UI.DialogueUI.Close();
        }

        [Test]
        public void CompanionWithProduce_CollectsBeforeTalking()
        {
            // star_slime is a companion that still produces (the behavioral/economic
            // split DECISION) — a ready collect must win over the dialogue line.
            var state = RichState();
            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "star_slime"));
            var slime = AnimalSpawner.Spawned.First(a => a.Def.id == "star_slime");
            Assert.IsTrue(slime.IsCompanion);

            string granted = null;
            GameEvents.OnItemGranted += id => granted = id;
            int before = state.Save.coins;
            slime.ForceProduceReady();
            slime.Interact(null);

            Assert.AreEqual("stardust", granted, "companion produce still fires ItemGranted");
            Assert.Greater(state.Save.coins, before, "companion produce still pays coins");
            UI.DialogueUI.Close();
        }

        // ------------------------------------------------------------- screenshots

        [UnityTest]
        public IEnumerator LivestockPlacementScreenshot_AnimalAtItsPlacedCell()
        {
            var state = RichState();
            const string region = "starlight_vale";
            Assert.IsTrue(_regions.TryGetBounds(region, out var bounds));
            var lighting = DayNightLighting.Create();
            _time.timeOfDay = 0.35f;   // daylight so the animal is clearly lit
            yield return null;

            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "chicken"));
            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "sheep"));
            var chicken = AnimalSpawner.Spawned.First(a => a.Def.id == "chicken" && a.RegionId == region);
            var sheep = AnimalSpawner.Spawned.First(a => a.Def.id == "sheep" && a.RegionId == region);

            // two livestock placed on cells of their own, in different ground tiers
            var lush = PlaceableCell(state, region, tier: 2, skip: 130);   // mid-ish of the lush core
            Assert.IsTrue(AnimalSpawner.TryPlace(state, _regions, region, "chicken", lush.x, lush.y));
            var mid = PlaceableCell(state, region, tier: 1, skip: 40);
            Assert.IsTrue(AnimalSpawner.TryPlace(state, _regions, region, "sheep", mid.x, mid.y));
            Debug.Log($"[P18Test] chicken placed at lush {lush}, sheep at mid-ring {mid}");
            yield return null;

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.transform.position = bounds.center + new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            DayNightLighting.Assign2DRenderer(cam);
            yield return null;

            Capture(cam, "phase18_livestock_placed");
            foreach (var (a, name) in new[] { (chicken, "chicken"), (sheep, "sheep") })
            {
                var rel = (a.transform.position - bounds.center) / (cam.orthographicSize * 2f);
                Debug.Log($"[P18Test] {name} at world {a.transform.position}, " +
                          $"pixel ≈ ({400 + rel.x * 800:F0}, {400 - rel.y * 800:F0}) " +
                          "in Logs/phase18_livestock_placed.png");
            }

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lighting.gameObject);
        }

        [UnityTest]
        public IEnumerator CompanionScheduleScreenshots_SameCompanionTwoPlacesTwoTimes()
        {
            var state = RichState();
            const string region = "starlight_vale";
            Assert.IsTrue(_regions.TryGetBounds(region, out var bounds));
            var lighting = DayNightLighting.Create();
            _time.timeOfDay = 0.30f;   // Morning
            yield return null;

            Assert.IsTrue(AnimalSpawner.TryBuy(state, _regions, "moon_rabbit"));
            var pet = AnimalSpawner.Spawned.First(a => a.Def.id == "moon_rabbit" && a.RegionId == region);
            pet.walkSpeed = 200f;
            yield return null;

            var camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 16f;   // covers home ring r=14.4 and work r=9.5
            cam.transform.position = bounds.center + new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            DayNightLighting.Assign2DRenderer(cam);

            Vector3 morningPos = default, nightPos = default;
            foreach (var (t, name) in new[] { (0.30f, "morning_work"), (0.92f, "night_home") })
            {
                _time.timeOfDay = t;
                for (int i = 0; i < 3000; i++)
                {
                    yield return null;
                    var target = CompanionSchedule.StopPosition(
                        CompanionSchedule.StopFor(_time.CurrentPeriod), bounds.center, PetAngle());
                    if ((pet.transform.position - target).magnitude < 0.05f) break;
                }
                yield return null;
                if (name == "morning_work") morningPos = pet.transform.position;
                else nightPos = pet.transform.position;

                Capture(cam, $"phase18_companion_{name}");
                var rel = (pet.transform.position - bounds.center) / (cam.orthographicSize * 2f);
                Debug.Log($"[P18Test] moon_rabbit at {name}: world {pet.transform.position}, " +
                          $"pixel ≈ ({400 + rel.x * 800:F0}, {400 - rel.y * 800:F0}) " +
                          $"in Logs/phase18_companion_{name}.png");
            }

            // Assert against the SCHEDULE STOPS, not the raw gap: the pet keeps idly
            // wandering within WanderRadius of wherever the schedule left it, so the
            // raw morning-to-night gap has legitimate slack in it. What must hold is
            // that each capture shows the pet at the stop its schedule called for, and
            // that those two stops are visibly far apart.
            var morningStop = CompanionSchedule.StopPosition(
                CompanionSchedule.StopFor(TimeManager.DayPeriod.Morning), bounds.center, PetAngle());
            var nightStop = CompanionSchedule.StopPosition(
                CompanionSchedule.StopFor(TimeManager.DayPeriod.Night), bounds.center, PetAngle());
            float leash = pet.WanderRadius + 0.1f;

            Assert.Greater((morningStop - nightStop).magnitude, 3f,
                "the two scheduled stops are visibly far apart");
            Assert.Less((morningPos - morningStop).magnitude, leash,
                "morning capture shows the pet at its Work stop");
            Assert.Less((nightPos - nightStop).magnitude, leash,
                "night capture shows the pet at its Home stop");
            Debug.Log($"[P18Test] moon_rabbit photographed at Work {morningPos} and Home {nightPos} " +
                      $"(stops {morningStop} / {nightStop}) ✓");

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lighting.gameObject);
        }

        static float PetAngle()
        {
            var companions = GameDatabase.Animals.Where(a => AnimalRoles.IsCompanion(a.id)).ToList();
            return CompanionSchedule.RingAngle(companions.FindIndex(a => a.id == "moon_rabbit"), companions.Count);
        }

        static void Capture(Camera cam, string name)
        {
            const int w = 800, h = 800;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Debug.Log($"[P18Test] captured Logs/{name}.png");
        }
    }
}
