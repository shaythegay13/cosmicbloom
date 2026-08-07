// P3-Phase 21 verification — legibility fixes for systems that already worked
// but were invisible to the player. Each test proves the SURFACE reflects REAL
// system state, not a plausible-looking string:
//
//   item 1  CropInfo reads grow time / day-night gate / stage out of crops.json
//           and PlotState, and reaches the screen via FarmActionPrompt.
//   item 2  The quest-giver "!" is driven by QuestEngine.StateForGiver — the same
//           predicate OnTalkedTo activates from — so it can never advertise a
//           quest that talking wouldn't start.
//   item 3  The produce sparkle tracks the Phase 4b/8 per-SPECIES clock, including
//           going dark on every instance once any one of them is collected.
//   item 4  INVESTIGATION ONLY: tap-to-plant lands on the tapped tile, not near
//           the player. No behavior was changed; this pins the current contract.
//   item 5  The Star Map carries each landmark's restoreCost / unlocks / done
//           state, and "rewilded" (FarmState.restoration) is now distinguishable
//           from landmark "restored" (FarmState.landmarksRestored).
//   item 6  The Crop/Tool buttons read as controls and show display names from
//           the first frame — never the placeholder "Tool"/"Crop", never a raw
//           snake_case id.
//
// The screenshot test renders a camera, so it belongs in the RENDER split, never
// the -nographics filter.
using System.Collections;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CosmicBloom.Tests
{
    public class Phase21LegibilityTests
    {
        RegionManager _regions;
        System.Collections.Generic.Dictionary<string, Grid> _grids;
        TimeManager _time;
        GameState _state;
        FarmingSystem _farming;
        QuestEngine _engine;

        const string Vale = "starlight_vale";

        [SetUp]
        public void SetUp()
        {
            GameEvents.Clear();
            GameDatabase.LoadAll();
            AnimalSpawner.ClearAll();
            AnimalController.ResetProduceClocks();
            NpcController.ResetMetFallback();

            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _time = new GameObject("TestTime").AddComponent<TimeManager>();
            _state = new GameState(new SaveSnapshot());
            _state.AddCoins(50000);
            _state.Save.level = 30;
            CropSelection.GrantStarterSeeds(_state);
            _farming = new FarmingSystem(_state);
            _engine = _regions.gameObject.AddComponent<QuestEngine>();
            _engine.Bind(_state);
            DialogueUI.Close();   // q_arrival's cutscene auto-opens on Bind
        }

        [TearDown]
        public void TearDown()
        {
            AnimalSpawner.ClearAll();
            AnimalController.ResetProduceClocks();
            if (_time != null) Object.DestroyImmediate(_time.gameObject);
            if (_grids != null) foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            // _engine shares _regions' GameObject - destroying that covers it.
            if (_regions != null) Object.DestroyImmediate(_regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
        }

        // ---------------------------------------------------- item 1: crop info

        [Test]
        public void CropInfo_ReportsStageGrowTimeAndWindow_StraightFromCropsJson()
        {
            // Pick the real data rows rather than asserting literal numbers here, so
            // this test tracks crops.json instead of duplicating it.
            var dayCrop = GameDatabase.Crops.First(c => c.region == Vale && c.growsAt == "day");
            var nightCrop = GameDatabase.Crops.First(c => c.region == Vale && c.growsAt == "night");

            _farming.Till(14, 14);
            _state.Save.seeds[dayCrop.id] = 5;
            Assert.IsTrue(_farming.Plant(14, 14, dayCrop.id), "planted the day crop");

            var info = CropInfo.For(_state, Vale, 14, 14);
            Assert.IsTrue(info.HasCrop, "info found the planted crop");
            Assert.AreEqual(dayCrop.name, info.CropName, "name from crops.json");
            Assert.AreEqual(dayCrop.stages, info.Stages, "stage count from crops.json");
            Assert.AreEqual(1, info.Stage, "freshly planted reads as stage 1, not 0");
            Assert.AreEqual(dayCrop.growMinutes, info.GrowMinutes, 0.001f, "grow time from crops.json");
            Assert.AreEqual("day", info.GrowsAt, "growth window from crops.json");
            Assert.IsTrue(info.NeedsWater, "a fresh planting needs its first watering");

            // all three requested facts must actually reach the rendered string
            StringAssert.Contains(dayCrop.name, info.Summary);
            StringAssert.Contains($"stage 1/{dayCrop.stages}", info.Summary, "current growth stage");
            StringAssert.Contains($"{dayCrop.growMinutes:0.#}m", info.Summary, "grow time");
            StringAssert.Contains("day only", info.Summary, "day/night gating");
            Debug.Log($"[P21Test] day-crop info: {info.Summary}");

            // A night crop at night is inside its window; the same crop by day is NOT,
            // and that difference has to be visible - it's the state that used to look
            // identical to "growing normally".
            _farming.Till(15, 14);
            _state.Save.seeds[nightCrop.id] = 5;
            Assert.IsTrue(_farming.Plant(15, 14, nightCrop.id), "planted the night crop");
            Assert.IsTrue(_farming.Water(15, 14), "watered so the window is the only gate left");

            _time.timeOfDay = 0.5f;    // midday
            var byDay = CropInfo.For(_state, Vale, 15, 14);
            _time.timeOfDay = 0.95f;   // night
            var byNight = CropInfo.For(_state, Vale, 15, 14);

            Assert.IsTrue(_time.IsNight, "0.95 is inside the night window");
            Assert.IsTrue(byDay.WindowShut, "night crop cannot advance during the day");
            Assert.IsFalse(byNight.WindowShut, "night crop can advance at night");
            StringAssert.Contains("waiting for nightfall", byDay.StatusNote,
                "a stalled night crop explains itself instead of looking broken");
            StringAssert.Contains("waiting for nightfall", byDay.Summary, "and it reaches the rendered string");
            Assert.AreEqual(string.Empty, byNight.StatusNote, "inside its window, nothing is blocking it");
            Assert.AreNotEqual(byDay.Summary, byNight.Summary,
                "the two states are visibly different on screen");
            Debug.Log($"[P21Test] night crop by day: {byDay.Summary}");
            Debug.Log($"[P21Test] night crop at night: {byNight.Summary}");
        }

        [UnityTest]
        public IEnumerator CropInfoLine_FollowsPlayer_AndShowsTheCropUnderThem()
        {
            FarmGrid.Create(Vale, _grids[Vale], _farming, _state);
            var player = PlayerController.Spawn(new Vector3(14.5f, 14.5f, 0f));
            var prompt = FarmActionPrompt.Create(player.transform);

            try
            {
                yield return null;
                Assert.IsNull(prompt.CurrentCropInfo, "bare tilled/untilled ground shows no crop info");

                var crop = GameDatabase.Crops.First(c => c.region == Vale);
                _farming.Till(14, 14);
                _state.Save.seeds[crop.id] = 5;
                Assert.IsTrue(_farming.Plant(14, 14, crop.id));
                yield return null;

                Assert.IsNotNull(prompt.CurrentCropInfo, "standing on a planted plot shows crop info");
                StringAssert.Contains(crop.name, prompt.CurrentCropInfo);
                StringAssert.Contains("stage 1/", prompt.CurrentCropInfo);
                Debug.Log($"[P21Test] prompt crop line: {prompt.CurrentCropInfo}");

                // The verb line is unchanged - Phase 17's contract still holds.
                Assert.AreEqual("Tap to water", prompt.CurrentLabel,
                    "the P17 verb line is untouched by the new info line");

                // Walk off the plot: the info line must clear, proving it tracks the tile
                // under the player rather than latching the last crop it saw.
                player.transform.position = new Vector3(20.5f, 20.5f, 0f);
                yield return null;
                Assert.IsNull(prompt.CurrentCropInfo, "info clears when the player leaves the plot");
            }
            finally
            {
                Object.DestroyImmediate(prompt.gameObject);
                Object.DestroyImmediate(player.gameObject);
            }
        }

        // ---------------------------------------------- item 2: quest-giver icon

        /// <summary>A quest whose giver can be chatted into starting it right now -
        /// exactly what CanStartFromGiver accepts.</summary>
        QuestDef FirstStartableSideQuest() =>
            GameDatabase.Quests.First(q => q.giver != null && _engine.CanStartFromGiver(q.giver, q));

        [UnityTest]
        public IEnumerator QuestGiverIcon_TracksRealAvailability_ThroughStartAndCompletion()
        {
            var quest = FirstStartableSideQuest();
            var giverDef = GameDatabase.Npc(quest.giver);
            Assert.IsNotNull(giverDef, $"{quest.id}'s giver {quest.giver} exists in npcs.json");

            var giver = NpcController.Spawn(giverDef, new Vector3(20, 20, 0), 0, 4, _state);
            try
            {
                yield return null;
                var icon = giver.GetComponent<QuestGiverIndicator>();
                Assert.IsNotNull(icon, "every spawned NPC carries the indicator component");

                icon.Refresh();
                Assert.AreEqual(QuestEngine.GiverState.Available, icon.Current,
                    "giver of a startable quest advertises Available");
                Assert.IsTrue(icon.IsShowing, "the icon is actually visible");
                Debug.Log($"[P21Test] {giverDef.id} shows Available for {quest.id}");

                // Start it for real, through the same event a chat fires.
                GameEvents.TalkedTo(giverDef.id);
                yield return null;
                Assert.IsTrue(_engine.IsActive(quest.id),
                    "the icon's promise held: talking really did start the quest");

                icon.Refresh();
                Assert.AreEqual(QuestEngine.GiverState.InProgress, icon.Current,
                    "once carried, the giver drops from Available to InProgress");
                Assert.IsTrue(icon.IsShowing, "still worth revisiting, so still visible");

                // Complete it out from under the icon and the icon must go dark.
                _state.Save.activeQuests.Remove(quest.id);
                _state.Save.questsCompleted.Add(quest.id);
                icon.Refresh();
                Assert.AreEqual(QuestEngine.GiverState.None, icon.Current,
                    "a finished giver has nothing left to advertise");
                Assert.IsFalse(icon.IsShowing, "icon hidden once there's nothing to offer");
                Debug.Log($"[P21Test] {giverDef.id} icon went Available -> InProgress -> None");
            }
            finally { Object.DestroyImmediate(giver.gameObject); }
        }

        [UnityTest]
        public IEnumerator QuestGiverIcon_StaysHiddenForNpcsWithNothingToGive()
        {
            // An NPC that gives no quest at all - the majority of the 50-strong roster.
            var giverIds = GameDatabase.Quests.Where(q => q.giver != null).Select(q => q.giver).ToHashSet();
            var plainDef = GameDatabase.Npcs.First(n => !giverIds.Contains(n.id));

            var npc = NpcController.Spawn(plainDef, new Vector3(20, 20, 0), 0, 4, _state);
            try
            {
                yield return null;
                var icon = npc.GetComponent<QuestGiverIndicator>();
                icon.Refresh();
                Assert.AreEqual(QuestEngine.GiverState.None, icon.Current);
                Assert.IsFalse(icon.IsShowing, $"{plainDef.id} gives no quest, so shows no icon");
                Debug.Log($"[P21Test] non-giver {plainDef.id} correctly shows nothing");
            }
            finally { Object.DestroyImmediate(npc.gameObject); }
        }

        [Test]
        public void QuestGiverAvailability_MatchesTheActivationRule_AcrossTheWholeRoster()
        {
            // The icon and the activation loop must agree for EVERY npc/quest pair - a
            // single divergence is a promise the game won't keep. This is the regression
            // guard for the two rules being kept in one predicate.
            foreach (var npc in GameDatabase.Npcs)
            {
                var state = _engine.StateForGiver(npc.id);
                bool anyStartable = GameDatabase.Quests.Any(q => _engine.CanStartFromGiver(npc.id, q));
                bool anyActive = GameDatabase.Quests.Any(q => q.giver == npc.id && _engine.IsActive(q.id));

                if (anyStartable) Assert.AreEqual(QuestEngine.GiverState.Available, state, npc.id);
                else if (anyActive) Assert.AreEqual(QuestEngine.GiverState.InProgress, state, npc.id);
                else Assert.AreEqual(QuestEngine.GiverState.None, state, npc.id);
            }
            int advertising = GameDatabase.Npcs.Count(n => _engine.StateForGiver(n.id) != QuestEngine.GiverState.None);
            Assert.Greater(advertising, 0, "at least one NPC has something to offer on a fresh save");
            Assert.Less(advertising, GameDatabase.Npcs.Count, "not every NPC advertises - the icon still discriminates");
            Debug.Log($"[P21Test] {advertising}/{GameDatabase.Npcs.Count} NPCs advertise a quest on a fresh save");
        }

        // ------------------------------------------- item 3: produce-ready icon

        [UnityTest]
        public IEnumerator ProduceIcon_FollowsTheSpeciesClock_AndClearsOnCollect()
        {
            var def = GameDatabase.Animals.First(a => a.id == "chicken");   // livestock with produce
            var animal = AnimalController.Spawn(def, new Vector3(18, 18, 0), _state, Vale, new Vector3(20, 20, 0));
            try
            {
                yield return null;
                var icon = animal.GetComponent<ProduceReadyIndicator>();
                Assert.IsNotNull(icon, "every spawned animal carries the indicator component");

                icon.Refresh();
                Assert.IsFalse(animal.ProduceReady, "clock was just started, nothing ready yet");
                Assert.IsFalse(icon.IsShowing, "no icon while the clock is still running");

                animal.ForceProduceReady();
                icon.Refresh();
                Assert.IsTrue(animal.ProduceReady);
                Assert.IsTrue(icon.IsShowing, "produce ready -> icon visible without tapping anything");
                Debug.Log($"[P21Test] {def.id} shows the produce sparkle when its clock is ready");

                // Collect for real; the clock reschedules and the icon must clear.
                animal.Interact(null);
                icon.Refresh();
                Assert.IsFalse(animal.ProduceReady, "collecting consumed the produce");
                Assert.IsFalse(icon.IsShowing, "icon clears once collected");
                Debug.Log($"[P21Test] {def.id} sparkle cleared after a real collect");
            }
            finally { Object.DestroyImmediate(animal.gameObject); }
        }

        [UnityTest]
        public IEnumerator ProduceIcon_IsPerSpecies_NotPerInstance()
        {
            // Phase 8's rule: one owned animal = one produce stream, however many
            // regions it stands in. The icon must not promise five collections.
            var def = GameDatabase.Animals.First(a => a.id == "chicken");
            var a1 = AnimalController.Spawn(def, new Vector3(18, 18, 0), _state, Vale, new Vector3(20, 20, 0));
            var a2 = AnimalController.Spawn(def, new Vector3(98, 18, 0), _state, "sun_garden", new Vector3(100, 20, 0));
            try
            {
                yield return null;
                var i1 = a1.GetComponent<ProduceReadyIndicator>();
                var i2 = a2.GetComponent<ProduceReadyIndicator>();

                a1.ForceProduceReady();
                i1.Refresh(); i2.Refresh();
                Assert.IsTrue(i1.IsShowing, "instance 1 shows ready");
                Assert.IsTrue(i2.IsShowing, "the shared species clock lights instance 2 as well");

                a1.Interact(null);   // collect from ONE instance
                i1.Refresh(); i2.Refresh();
                Assert.IsFalse(i1.IsShowing, "collected instance goes dark");
                Assert.IsFalse(i2.IsShowing,
                    "so does the other region's instance - one stream, not five (Phase 8 rule)");
                Debug.Log("[P21Test] produce icon honours the per-species clock across 2 regions");
            }
            finally
            {
                Object.DestroyImmediate(a1.gameObject);
                Object.DestroyImmediate(a2.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator ProduceIcon_StaysHiddenForAnimalsWithNoProduceInData()
        {
            var def = GameDatabase.Animals.First(a => string.IsNullOrEmpty(a.produce) || a.produceMinutes <= 0);
            var animal = AnimalController.Spawn(def, new Vector3(18, 18, 0), _state, Vale, new Vector3(20, 20, 0));
            try
            {
                yield return null;
                var icon = animal.GetComponent<ProduceReadyIndicator>();
                icon.Refresh();
                Assert.IsFalse(icon.IsShowing, $"{def.id} has no produce in data, so never shows the icon");
                Debug.Log($"[P21Test] produce-less {def.id} correctly shows nothing");
            }
            finally { Object.DestroyImmediate(animal.gameObject); }
        }

        // -------------------------------------------- item 4: INVESTIGATION ONLY

        [UnityTest]
        public IEnumerator Investigation_TapToPlant_LandsOnTheTappedTile_NotNearThePlayer()
        {
            // No behavior changed here. This pins the current contract so the "walking
            // paths" question can be answered from evidence: if the tapped tile is
            // always the planted tile, imprecise targeting is not the problem.
            FarmGrid.Create(Vale, _grids[Vale], _farming, _state);
            var player = PlayerController.Spawn(new Vector3(20.5f, 20.5f, 0f));

            try
            {
                // Deliberately tap a tile several cells away from where the player stands.
                const int Tx = 14, Ty = 12;
                var start = player.transform.position;
                Assert.Greater(Vector2.Distance(start, new Vector2(Tx + 0.5f, Ty + 0.5f)), 5f,
                    "target is well outside interact range, so any 'near the player' bug would show");

                var tile = new Vector3(Tx + 0.5f, Ty + 0.5f, 0f);
                player.OnTapWorld(tile);
                for (int i = 0; i < 2000 && player.MoveTarget != null; i++) player.MoveStep(0.05f);
                yield return null;

                Assert.IsNotNull(_farming.PlotAt(Tx, Ty), "the TAPPED cell was tilled");
                Assert.IsTrue(_farming.PlotAt(Tx, Ty).tilled);

                player.OnTapWorld(tile);   // plant
                for (int i = 0; i < 2000 && player.MoveTarget != null; i++) player.MoveStep(0.05f);
                yield return null;

                var planted = _farming.PlotAt(Tx, Ty);
                Assert.IsNotNull(planted.crop, "the TAPPED cell was planted");

                // And nothing landed on the cell the player happened to be standing on.
                var underPlayer = _farming.PlotAt(20, 20);
                Assert.IsNull(underPlayer,
                    "nothing was tilled or planted under the player - the tap targeted the tapped tile");

                // plant has no tiered area, so it is exactly one cell by construction
                var plantArea = ToolService.AreaFor("plant", 3, Tx, Ty).ToList();
                Assert.AreEqual(1, plantArea.Count, "plant is always a single cell at any tool tier");
                Assert.AreEqual(new Vector2Int(Tx, Ty), plantArea[0], "and that cell is the tapped one");
                Debug.Log($"[P21Test] INVESTIGATION: tap at ({Tx},{Ty}) tilled+planted ({Tx},{Ty}); " +
                          $"player stood at {start}; plant area = 1 cell. Targeting is exact.");
            }
            finally
            {
                Object.DestroyImmediate(player.gameObject);
            }
        }

        // ------------------------------------------ item 5: landmark signposting

        [UnityTest]
        public IEnumerator StarMap_SignpostsEveryLandmarkCostUnlockAndProgress()
        {
            var hud = HudUI.Create(_state, _engine, _regions);
            try
            {
                hud.Map.Toggle();
                yield return null;

                // Restore one real landmark so the done/todo split is exercised, not assumed.
                var valeDef = GameDatabase.Region(Vale);
                var firstLm = valeDef.landmarks[0];
                _state.FarmFor(Vale).landmarksRestored.Add(firstLm.id);
                hud.Map.Refresh();
                yield return null;

                foreach (var def in GameDatabase.Regions)
                {
                    var row = GameObject.Find($"Region_{def.id}");
                    Assert.IsNotNull(row, $"map row for {def.id}");
                    string text = row.GetComponentInChildren<Text>().text;

                    int done = def.landmarks.Count(l => _state.FarmFor(def.id).landmarksRestored.Contains(l.id));
                    StringAssert.Contains($"Landmarks restored {done}/{def.landmarks.Count}", text,
                        $"{def.id} shows landmark progress");

                    foreach (var lm in def.landmarks)
                    {
                        StringAssert.Contains(lm.name, text, $"{def.id} lists {lm.id} by name");
                        StringAssert.Contains(lm.unlocks.Replace('_', ' '), text,
                            $"{def.id} shows what {lm.id} unlocks");
                        if (!_state.FarmFor(def.id).landmarksRestored.Contains(lm.id))
                            StringAssert.Contains($"{lm.restoreCost} coins", text,
                                $"{def.id} shows {lm.id}'s restoreCost before you ever find it");
                    }
                    Debug.Log($"[P21Test] map row {def.id}:\n{text}");
                }

                // the restored one reads as restored, and no longer quotes a price
                string valeText = GameObject.Find($"Region_{Vale}").GetComponentInChildren<Text>().text;
                StringAssert.Contains($"{firstLm.name} — restored", valeText);
                StringAssert.Contains("Landmarks restored 1/3", valeText);
            }
            finally { DestroyHud(hud); }
        }

        [UnityTest]
        public IEnumerator RewildingAndLandmarkRestoration_AreNoLongerTheSameWord()
        {
            var hud = HudUI.Create(_state, _engine, _regions);
            try
            {
                _state.FarmFor(Vale).restoration = 42f;
                _state.FarmFor(Vale).landmarksRestored.Add(GameDatabase.Region(Vale).landmarks[0].id);
                yield return null;   // HUD Update

                string stats = GameObject.Find("TopBar").GetComponentInChildren<Text>().text;
                StringAssert.Contains("42% rewilded", stats,
                    "the farm meter is labelled 'rewilded' (FarmState.restoration)");
                StringAssert.DoesNotContain("restored", stats,
                    "the HUD no longer uses 'restored' for the rewilding meter");
                Debug.Log($"[P21Test] HUD: {stats}");

                hud.Map.Toggle();
                yield return null;
                string map = GameObject.Find($"Region_{Vale}").GetComponentInChildren<Text>().text;

                // Both words appear on the map, but on different, unambiguous things.
                StringAssert.Contains("42% rewilded", map, "region meter = rewilded");
                StringAssert.Contains("Landmarks restored", map, "landmark count = restored");
                Debug.Log("[P21Test] 'rewilded' (crop meter) and 'restored' (landmarks) are now distinct");
            }
            finally { DestroyHud(hud); }
        }

        // --------------------------------------- item 6: selector button labels

        [UnityTest]
        public IEnumerator SelectorButtons_ReadAsControls_WithDisplayNames_OnTheVeryFirstFrame()
        {
            var hud = HudUI.Create(_state, _engine, _regions);
            try
            {
                // Read BEFORE any Update runs - the old code created "Tool"/"Crop" here
                // and only fixed them inside the first Update, so the functional label
                // was never actually on screen.
                var toolLabel = GameObject.Find("Tool").GetComponentInChildren<Text>();
                var cropLabel = GameObject.Find("Crop").GetComponentInChildren<Text>();

                Assert.AreNotEqual("Tool", toolLabel.text, "never the bare placeholder");
                Assert.AreNotEqual("Crop", cropLabel.text, "never the bare placeholder");
                StringAssert.StartsWith("Tool:", toolLabel.text, "reads as a control");
                StringAssert.StartsWith("Crop:", cropLabel.text, "reads as a control");
                Debug.Log($"[P21Test] frame 0 labels: '{toolLabel.text}' / '{cropLabel.text}'");

                yield return null;

                // The tool label shows tools.json's display name, never the raw id.
                string toolId = _state.Save.tools[0];
                var toolDef = GameDatabase.Tools.First(t => t.id == toolId);
                Assert.AreNotEqual(toolId, toolDef.name, "this tool's id and name really do differ");
                StringAssert.Contains(toolDef.name, toolLabel.text, "shows the display name");
                StringAssert.DoesNotContain(toolId, toolLabel.text, "never the raw snake_case id");

                var cropDef = GameDatabase.Crop(CropSelection.SelectedFor(_state, Vale));
                StringAssert.Contains(cropDef.name, cropLabel.text, "shows the crop's display name");
                Debug.Log($"[P21Test] after update: '{toolLabel.text}' / '{cropLabel.text}'");
            }
            finally { DestroyHud(hud); }
        }

        [UnityTest]
        public IEnumerator ToolButton_CyclesThroughEveryOwnedTool_NotJustTheFirstTwo()
        {
            // Regression guard: the old CycleTool recovered its position by parsing the
            // label back out, which silently reset to owned[0] every tap once the label
            // stopped being a bare id - leaving most owned tools unreachable.
            _state.Save.tools.Clear();
            foreach (var t in new[] { "cosmic_hoe", "watering_can", "star_scythe", "fishing_rod" })
                _state.Save.tools.Add(t);

            var hud = HudUI.Create(_state, _engine, _regions);
            try
            {
                yield return null;
                var toolButton = GameObject.Find("Tool").GetComponent<Button>();
                var toolLabel = toolButton.GetComponentInChildren<Text>();

                var seen = new System.Collections.Generic.List<string> { toolLabel.text };
                for (int i = 0; i < _state.Save.tools.Count; i++)
                {
                    toolButton.onClick.Invoke();
                    yield return null;
                    seen.Add(toolLabel.text);
                }

                var distinct = seen.Distinct().ToList();
                Assert.AreEqual(_state.Save.tools.Count, distinct.Count,
                    $"every owned tool is reachable by cycling; saw [{string.Join(" | ", seen)}]");
                foreach (var id in _state.Save.tools)
                {
                    var name = GameDatabase.Tools.First(t => t.id == id).name;
                    Assert.IsTrue(distinct.Any(s => s.Contains(name)), $"{id} ({name}) appeared in the cycle");
                }
                Debug.Log($"[P21Test] tool cycle reached all {distinct.Count} owned tools: {string.Join(" -> ", seen)}");
            }
            finally { DestroyHud(hud); }
        }

        // ------------------------------------------ visual evidence (RENDER split)

        [UnityTest]
        public IEnumerator Phase21_LegibilityScreenshots()
        {
            FarmGrid.Create(Vale, _grids[Vale], _farming, _state);
            var hud = HudUI.Create(_state, _engine, _regions);
            var player = PlayerController.Spawn(new Vector3(14.5f, 14.5f, 0f));
            var prompt = FarmActionPrompt.Create(player.transform);

            // a planted crop under the player (info line), a quest giver (!), and a
            // livestock animal with produce ready (sparkle) - all in one frame
            var crop = GameDatabase.Crops.First(c => c.region == Vale);
            _farming.Till(14, 14);
            _state.Save.seeds[crop.id] = 5;
            _farming.Plant(14, 14, crop.id);

            var quest = FirstStartableSideQuest();
            var giver = NpcController.Spawn(GameDatabase.Npc(quest.giver), new Vector3(20, 20, 0), 0, 4, _state);
            giver.transform.position = new Vector3(12.6f, 14.6f, 0f);

            var chicken = GameDatabase.Animals.First(a => a.id == "chicken");
            var animal = AnimalController.Spawn(chicken, new Vector3(16.4f, 14.4f, 0f), _state, Vale, new Vector3(20, 20, 0));
            animal.ForceProduceReady();

            GameObject camGo = null;
            try
            {
                yield return null;
                giver.GetComponent<QuestGiverIndicator>().Refresh();
                animal.GetComponent<ProduceReadyIndicator>().Refresh();
                animal.transform.position = new Vector3(16.4f, 14.4f, 0f);   // undo any wander drift
                DialogueUI.Close();
                yield return null;

                camGo = new GameObject("CaptureCam") { tag = "MainCamera" };
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 4.2f;   // 3.2 cropped the labels at the frame edges
                cam.transform.position = new Vector3(14.5f, 14.6f, -10f);
                cam.backgroundColor = new Color(0.06f, 0.05f, 0.12f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                yield return null;

                Capture(cam, 720, 720, "phase21_world_indicators.png");

                // -- HUD + Star Map, with a landmark already restored so both states show.
                // The world actors are hidden first: this capture forces the canvas to
                // ScreenSpaceCamera (Overlay canvases don't render into a RenderTexture),
                // which lets world-space TextMesh draw over the panel. In the real game
                // the canvas is Overlay and always wins, so leaving them visible would put
                // an artifact in the evidence that the shipping build never shows.
                prompt.gameObject.SetActive(false);
                player.gameObject.SetActive(false);
                giver.gameObject.SetActive(false);
                animal.gameObject.SetActive(false);

                _state.FarmFor(Vale).restoration = 42f;
                _state.FarmFor(Vale).landmarksRestored.Add(GameDatabase.Region(Vale).landmarks[0].id);
                hud.Map.Toggle();
                hud.Map.Refresh();

                var canvas = UIRoot.Canvas;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                yield return null;
                yield return null;

                Capture(cam, 720, 1280, "phase21_starmap_landmarks.png");
                Debug.Log("[P21Test] captured Logs/phase21_world_indicators.png + Logs/phase21_starmap_landmarks.png");
            }
            finally
            {
                if (camGo != null) Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(animal.gameObject);
                Object.DestroyImmediate(giver.gameObject);
                Object.DestroyImmediate(prompt.gameObject);
                Object.DestroyImmediate(player.gameObject);
                DestroyHud(hud);
            }
        }

        // ------------------------------------------------------------- helpers

        static void Capture(Camera cam, int w, int h, string fileName)
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
            Object.DestroyImmediate(rt);
        }

        static void DestroyHud(HudUI hud)
        {
            if (hud == null) return;
            Object.DestroyImmediate(hud.QuestLog.gameObject);
            Object.DestroyImmediate(hud.Map.gameObject);
            Object.DestroyImmediate(hud.Pause.gameObject);
            Object.DestroyImmediate(hud.Ranch.gameObject);
            Object.DestroyImmediate(hud.gameObject);
        }
    }
}
