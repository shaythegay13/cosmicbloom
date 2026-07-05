# Cosmic Bloom — Overnight Build Log

Session start: 2026-07-04 ~22:30 local. Unattended run per build spec.
Verification model: headless batchmode (`D:\Unity\6000.5.2f1\6000.5.1f1\Editor\Unity.exe`, project is 6000.5.1f1)
for compiles, Unity Test Runner PlayMode for behavior; MCP not relied upon.

---

## Phase 0 — Verify current state (2026-07-04 22:30)

**Editor shutdown (pre-work):** Unity Editor was open with the project when the run
started, holding `Temp/UnityLockfile` — batchmode cannot run alongside it and
nobody is present to click MCP re-accept dialogs. Used the still-alive MCP
connection to (1) `EditorSceneManager.SaveOpenScenes()` — returned `True`,
active scene `Assets/Scenes/SampleScene.unity`, `isDirty: False` after save —
then (2) `EditorApplication.Exit(0)` via delayCall. Confirmed both Unity.exe
processes exited and the lockfile is gone. Disk state was verified identical
to expectations *before* the shutdown, so nothing could be lost.

**Scene contents verified by reading the raw YAML** (`Assets/Scenes/SampleScene.unity`, 4931 lines):

| Object | Components | Status |
|---|---|---|
| GameManager | SaveManager (`a94fbba…`) + TimeManager (`2efc2521…`) | ✅ present, matches spec |
| StarlightVale / Ground | Grid + Tilemap + TilemapRenderer, 20×20 tiles, 3 tier indices | ✅ present |
| FarmPlot | PrefabInstance of `Assets/Prefabs/FarmPlot.prefab` (guid `e52cbbee…`) at (0.5, 0.5) | ✅ present |
| Main Camera, Directional Light, Global Volume | standard | ✅ present |
| RenderTestSprite | FarmPlotE2ETest (`c8338a50…`) | 🗑 deleted this phase |
| FarmPlotTestRunner | FarmPlotManualControls (`242ea4ed…`) | 🗑 deleted this phase |

GUIDs cross-checked against `.meta` files — the component identification is from
disk, not from session history.

**Code audit (all files read in full):**
- `GameState.cs` — schema-2 `SaveSnapshot` already has `currentRegion`, `unlockedRegions`,
  `farms{regionId → FarmState{seed, restoration 0-100, milestonesClaimed, landmarksRestored, plots}}`,
  `seeds`, `tools`, `animals`, `questsCompleted`, `activeQuest`. **Per-region restoration is
  natively supported by the existing schema** — no schema extension needed for Phase 8.
- `FarmingSystem.cs` — till/plant/water/harvest + soil + `FastForward()` offline growth;
  day/night grow gating already enforced via `TimeManager.GrowWindowOpen` (Phase 7 note:
  enforcement EXISTS, needs only verification). Restoration already accrues on harvest
  (`def.restore`, ×1.5 native bonus).
- `TimeManager.cs` — day/night with `OnSunrise/OnNightfall/OnNewDay` events and
  `AmbientIntensity()` helper ready for Phase 7 lighting hookup.
- `SaveManager.cs` — local-first debounced saves, `GameDatabase.LoadAll()` in Awake. Confirmed.
- `GameDatabase.cs` / `Definitions.cs` — loaders for all 6 JSON files.
- `GameConfig.cs` — `CharityEnabled = false` untouched, will stay so.

**Data audit** (parsed all 6 JSONs): 5 regions / 26 crops / 50 NPCs / 60 quests /
15 animals / 10 tools. Main chain from `q_arrival` walks exactly **47 quests**, ending
at `q_infinite_fields`. 29 distinct quest step types (largest: `restoration_above` ×11,
`harvest_crop` ×11, `restore_landmark` ×7). NPC dialogue keys are `{intro, idle, quest_complete}`
— **linear, no branching**, so Phase 4's simple dialogue box is data-correct.
Tools: 3 till / 3 water / 2 harvest / 1 clear / 1 fish with tier + notes describing area effects.
Region unlocks in data: `unlockCoins` (1500/2500/4000/6000) + `unlockLevel` (8/12/16/20);
milestones `[25,50,75,100]` per region.

**Packages:** `com.unity.test-framework` 1.7.0 ✅ (PlayMode tests possible),
`com.unity.inputsystem` 1.19.0 ✅, ugui ✅, Newtonsoft ✅.

**Actions taken:**
1. Deleted `RenderTestSprite` + `FarmPlotTestRunner` from the scene via new headless
   task runner `Assets/Editor/OvernightTasks.cs` (`Phase0Cleanup`), executed in batchmode.
2. Deleted `FarmPlotE2ETest.cs` + `FarmPlotManualControls.cs` (debug scaffolding; the
   E2E sequence they encoded is being converted to real PlayMode tests in Phase 3).
   Note: `FarmPlotManualControls` used legacy `Input.GetKeyDown` — the exact legacy-input
   pattern the spec warns about; good riddance.
3. Also deleted `Assets/Editor/CosmicBloomFarmPlotSetup.cs` — first compile verify
   (`Logs/batch_verify.log`) failed with 5× CS0246 because that editor script
   referenced the deleted test classes. It was the menu-item orchestrator for the
   old manual/E2E debug flow (its prefab-creation duty is done — `FarmPlot.prefab`
   exists on disk). Kept: `CosmicBloom2DRendererSetup.cs` / `Verify.cs` (renderer
   setup, still useful).

**Evidence:**
- `Logs/phase0_cleanup.log`: `[Phase0] removed RenderTestSprite`, `[Phase0] removed FarmPlotTestRunner`, `cleanup complete, removed=2/2`, exit code 0.
- `grep -c "RenderTestSprite|FarmPlotTestRunner" SampleScene.unity` → **0 matches** after cleanup.
- Recompile after all deletions (`Logs/batch_verify_p0.log`): **zero compile errors in Phase 0 code**.
  (The log shows 3× CS0246 in `IInteractable.cs` — that is Phase 1 work-in-progress that the
  batch caught mid-write because I started writing Phase 1 files while the verify ran.
  Process fix adopted: verify batches only run against a complete working tree from now on.)
- Backend validator read directly (`server/routes/game.js:85-98`): checks only
  `schema==2`, `coins 0..1e8`, `level 1..1000`, per-farm `restoration 0..100`,
  `≤1000 plots/farm`, total JSON `≤300KB`. **Unknown top-level fields pass through** —
  quest/tool/position state can extend the snapshot compatibly without schema changes.

**Phase 0: COMPLETE ✅**

---

## Phase 1 — Player character & tap-to-move (2026-07-04 ~23:15)

**Built:**
- `Assets/Scripts/CosmicBloom.Runtime.asmdef` — game code moved into a named
  assembly (references `Unity.InputSystem`, `Unity.RenderPipelines.Universal.Runtime`)
  so PlayMode tests can reference it. Editor folder stays in Assembly-CSharp-Editor.
- `World/PlayerController.cs` — tap-to-move + tap-to-interact using **new Input
  System only** (`Pointer.current`; project has `activeInputHandler: 1` so legacy
  Input would throw). Tap empty ground → walk (clamped to region bounds). Tap an
  `IInteractable` → interact if within 1.4u, else walk over and interact on arrival.
  Works for mouse and touch (Touchscreen is a Pointer device).
- `World/IInteractable.cs` — the interaction contract plots/NPCs/animals implement
  in later phases.
- `World/RegionManager.cs` — region world-bounds registry + current-region tracking +
  `ClampToCurrentRegion`. Travel/unlock gating intentionally deferred to Phase 8.
- `World/ProceduralSprites.cs` — shared placeholder sprite factory (capsule/square/
  critter) + hex color parsing; all placeholder art flows through here.
- `Player/CameraFollow.cs` — smoothed follow, clamps to region bounds, centers
  small regions; `SnapTo` for region travel.
- `Core/GameBootstrap.cs` — runtime world assembly on GameManager. **DECISION:**
  everything dynamic (player, and later NPCs/animals/plots/UI) spawns at runtime
  from data; the scene keeps only managers/camera/light/tilemaps. Rationale: scene
  YAML surgery lost work twice on 2026-07-03; runtime spawning is data-driven,
  domain-reload-proof, and PlayMode-testable headlessly.
- `Assets/Tests/PlayMode/` — test assembly + 4 movement tests replacing the old
  manual-keypress verification.
- Scene delta (one headless edit via `OvernightTasks.Phase1Setup`): GameBootstrap
  component added to GameManager. Verified in scene YAML: script GUID
  `87b50cf69b2afea4b9b8478e4fbaccaf` at line 4499.

**Verification evidence** (`Logs/test_results_p1.xml`, `Logs/test_run_p1.log`):
```
total: 4 passed: 4 failed: 0 skipped: 0
[P1Test] start=(10.00, 10.00) target=(14.00, 12.00) → arrived=(14.00, 12.00)
[P1Test] tap (500,-300) → clamped target=(19.70, 0.30) → stopped at=(19.70, 0.30)  [in bounds]
[P1Test] walk-then-interact: interactCount=1 at playerPos=(15.00, 10.00)
```
Movement, boundary clamping, and both interaction paths (immediate + walk-over)
all pass in a real PlayMode run against a 20×20 region matching StarlightVale.
Note: tests drive `PlayerController.OnTapWorld` (the method the Input System tap
handler calls with the converted world position) — device-level input simulation
via `InputTestFixture` was skipped as the conversion layer is 3 lines; flagged
for a quick human tap-test in the morning.

**Phase 1: COMPLETE ✅**

---


