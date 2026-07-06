# Cosmic Bloom — Overnight Build Log

---

## Phase 4b — animal shared-pool/exclusive model, replacing Phase 4 (2026-07-05 ~21:10)

**Phase 4's one-home-region model is superseded, not layered on top** — see
"SUPERSEDED" marker on the Phase 4 entry directly below. Per the new spec: most
animals should be available in every region; a small number are region-exclusive.

**Confirmed from data before assigning anything:** `regions.json`'s
`unlockCoins`/`unlockLevel` per region — starlight_vale 0/1, sun_garden 1500/8,
moon_garden 2500/12, star_garden 4000/16, **nebula_garden 6000/20**. Nebula
Garden is last in progression by both metrics, so it's the one region that gets
**two** exclusives instead of one (4×1 + 1×2 = 6, matching the spec's 6
region-exclusive animals out of 15).

**DECISION — the model:**
- `AnimalSpawner.ExclusiveRegionByAnimal` replaces `HomeRegionByAnimal` entirely
  (same file, same code-side-lookup pattern, different shape): a
  `Dictionary<string,string>` maps only the 6 **exclusive** animals to their one
  region. Any animal **not** in the dictionary is shared — spawnable in all 5.
- Exclusives, reusing Phase 4's findings where the signal already existed:
  - **Sun Garden** ← `asteroid_pig` (Phase 4's exact palette hex match, accent
    `#8a6a3e` = Sun Garden's ground tier — unchanged).
  - **Moon Garden** ← `crater_goat` (Phase 4's exact palette hex match, accent
    `#7d84b8` = Moon Garden's ground tier — unchanged).
  - **Starlight Vale** ← `glow_axolotl` (its notes — "Lives in the restored
    Moonlit Pond" — read as more definitive residency than `comet_duck`'s
    "Happiest paddling" preference wording, so it gets the Vale's one exclusive
    slot; `comet_duck` moves to the shared pool).
  - **Star Garden** ← `star_owl` (Phase 4's name/theming call — constellation/
    stargazing — unchanged; no palette match existed for this region either
    phase).
  - **Nebula Garden** ← `nebula_cow` **and** `star_slime` (Phase 4's pairing —
    name + magenta palette family, and `star_slime` shares `nebula_cow`'s exact
    accent hex `#e05ad8` — unchanged; this region's second exclusive slot is
    exactly the extra one the progression-order data calls for).
- **Shared pool (9):** `chicken`, `sheep`, `comet_duck`, `moon_rabbit`,
  `biolume_fox`, `cosmic_tortoise`, `bee_swarm`, `butterfly_swarm`,
  `firefly_swarm`. (`comet_duck` and `moon_rabbit` were Vale/Moon-assigned under
  Phase 4's weaker theming signal; both lost their slot to a stronger-signal
  animal once each region was capped at one exclusive, so they fall back to
  shared — still fully consistent with their own flavor text, just not
  exclusive to one place.)

**Replaced spawn logic:** `AnimalSpawner.CanSpawnInRegion(animalId, regionId)`
is the single source of truth (shared → always true; exclusive → true only for
its one region). `SpawnIntoEligibleRegions` loops every region in
`GameDatabase.Regions` and calls `TrySpawnOne` per region, which checks
`CanSpawnInRegion` first — rejecting (returning `false`, spawning nothing) for
ineligible regions. `SpawnOwned`/`TryBuy` both call this instead of the old
single-region lookup, so **one purchase now produces one instance per eligible
region** — 5 instances for a shared animal, 1 for an exclusive.

**Known implication, flagged not fixed:** shared animals now produce coins from
up to 5 simultaneous instances instead of 1 — up to a 5× income multiplier on
those 9 animals' produce loop versus the pre-Phase-4b game. This is a direct,
intended consequence of the user-specified model ("spawnable in all 5
regions"), not a bug — flagging it here as a balance-relevant fact for whoever
next tunes economy numbers, not deciding unilaterally to counter-balance it.

**Verification evidence:**
- Compile: `Logs/phase5_compile.log` — 0 `error CS` lines.
- New tests (`Assets/Tests/PlayMode/AnimalRegionTests.cs`, fully rewritten, 3
  cases): `Logs/test_results_p5animals.xml`, **3/3 PASS**.
  - `SharedExclusiveSplit_MatchesSpec` (data-shape guard, no scene): iterates
    all 15 animals × all 5 `regions.json` ids through `CanSpawnInRegion`,
    asserts exactly 9 shared / 6 exclusive, and exactly 2 exclusives land on
    `nebula_garden` vs. 1 each elsewhere — a living regression check of the
    spec's own numbers, not just this phase's hand-picked examples.
  - `SharedAnimal_SpawnsInEveryRegion`: buys `chicken`, reads back real
    `AnimalController.transform.position` for every spawned instance against
    real `RegionManager` bounds (`RegionWorldBuilder.BuildAll`, not asserted
    bounds) — **5 instances, landing inside all 5 region bounds** (spec only
    required "at least two").
  - `ExclusiveAnimal_OnlyInAssignedRegion_RejectedElsewhere`: confirms
    `CanSpawnInRegion("crater_goat", ...)` is **false** for Sun Garden and the
    Vale, **true** only for Moon Garden; then buys it and confirms exactly **1**
    real spawned instance, positioned inside Moon Garden's bounds and absent
    from Sun Garden's and the Vale's.
- Full-suite regression (same nographics/render split as Phases 2–4, with the
  3 new tests replacing Phase 4's 1 in the nographics filter list): **56/56
  PASS** (`Logs/p5_split_nographics.xml`) + **5/5 PASS**
  (`Logs/p5_split_render.xml`) — **61/61 total, 0 failures.**

**Phase 4b: COMPLETE.**

---

## Phase 4 — animal region distribution (2026-07-05 ~20:40) — **SUPERSEDED by Phase 4b above (2026-07-05 ~21:10) — kept for history, do not build on this model**

Original entry preserved as-written below; `AnimalSpawner.HomeRegionByAnimal`
no longer exists in code as of Phase 4b.

**Research (before deciding anything):** `animals.json` has no region/home field.
`regions.json` and `AnimalSpawner.cs` confirmed all 15 owned animals spawned into
a single Vale paddock regardless of which animal (`AnimalSpawner.cs`'s own header
comment already flagged this as a DECISION pending review). `COSMIC_BLOOM_GDD.md`
gives only two explicit region ties (both to Starlight Vale's Moonlit Pond
landmark, via `comet_duck` and `glow_axolotl`'s own `notes` fields) plus Act 3's
four Sacred Gardens' one-line themes (Sun: heat/solar; Moon: glow-in-the-dark/
nocturnal; Star: constellation/stargazing; Nebula: bioluminescent/floating
islands) — no per-animal assignment. Palette hex-compared against each region's
`groundTiers`: two **exact** hex matches turned up (`crater_goat`'s accent
`#7d84b8` = Moon Garden's 3rd ground tier; `asteroid_pig`'s accent `#8a6a3e` =
Sun Garden's 1st ground tier), which reads as an intentional but unlabeled
authoring signal.

**DECISION: code-side lookup table, not a new JSON field** — same pattern as
`ExploreTargets`' position table (Phase 2). A `Dictionary<string,string>` in
`AnimalSpawner.HomeRegionByAnimal` assigns each of the 15 animals from, in
priority order: (1) explicit landmark notes, (2) exact palette hex match to a
region's `groundTiers`, (3) name/behavior theming against the Sacred Gardens'
one-line themes, falling back to `starlight_vale` when nothing pulls elsewhere.
Result: 9 stay in Starlight Vale (`chicken`, `sheep` — GDD's own "farm classics"
pairing, both below any Act-3 region's `unlockLevel`; `comet_duck`, `glow_axolotl`
— explicit Moonlit Pond notes; `biolume_fox` — free Act-1 tutorial companion;
`cosmic_tortoise` — body `#6fae62` near-matches Vale's `#5fae62` lush tier;
`bee_swarm`/`butterfly_swarm`/`firefly_swarm` — pollinator/helper roster with no
region-specific theming, kept on the main farm); `asteroid_pig` → Sun Garden;
`crater_goat`, `moon_rabbit` → Moon Garden (goat by exact hex, rabbit by its
nocturnal notes matching Moon Garden's nocturnal-farming theme); `star_owl` →
Star Garden (constellation/stargazing); `nebula_cow`, `star_slime` → Nebula
Garden (cow by name + magenta palette family; slime shares the cow's exact
accent hex `#e05ad8`).

**Known limitation, flagged not fixed:** `AnimalSpawner.TryBuy` only gates on
`unlockLevel`/coins, not on the assigned region being unlocked (unchanged from
before this phase). Four Act-3 animals can be bought before their home region is
reachable (biggest gap: `nebula_cow` unlocks at level 9, Nebula Garden at level
20) — cosmetically fine since `RegionWorldBuilder.BuildAll` always builds and
registers all 5 regions' bounds regardless of unlock state, so the animal just
waits there for the player to arrive. Not fixed because purchase-gating on
region-unlock wasn't asked for and would change existing `TryBuy` behavior.

`AnimalSpawner.SpawnOne` now paddocks animals per-region (own index counter per
`regionId`) instead of one global index, so animals in different regions don't
stack at the same offset.

**Verification evidence:**
- Compile: `Logs/phase4_compile.log` — 0 `error CS` lines (pre-dates the new
  test file but validates `AnimalSpawner.cs`'s own edit; the full-suite runs
  below recompiled everything together with 0 errors either way).
- New test (`Assets/Tests/PlayMode/AnimalRegionTests.cs`,
  `NonValeAnimals_SpawnInAssignedRegionBounds`): buys `crater_goat`,
  `asteroid_pig`, `star_owl`, `nebula_cow`, `chicken` at level 20 and reads back
  **actual `AnimalController.transform.position`** against each region's real
  `RegionManager` bounds (built by `RegionWorldBuilder.BuildAll`, not asserted
  bounds) — `Logs/test_results_p4animals.xml`, **1/1 PASS**. Confirms
  `crater_goat`→moon_garden, `asteroid_pig`→sun_garden, `star_owl`→star_garden,
  `nebula_cow`→nebula_garden, and `chicken` still in starlight_vale, all by
  reading the spawned GameObjects' real world position, not the spawner's
  intended behavior.
- Full-suite regression (same nographics/render split as Phase 2/3, with the
  new test added to the nographics filter list): **54/54 PASS**
  (`Logs/p4_split_nographics_v3.xml`) + **5/5 PASS** (`Logs/p4_split_render.xml`)
  — **59/59 total, 0 failures.**

**Three batchmode pitfalls hit and fixed along the way** (added to the
`headless-unity-verify-pipeline` memory so they aren't repeated):
1. First test run combined `-quit` with `-runTests` — Unity compiled, then quit
   immediately without ever entering the test runner (exit code 0, no error
   logged; silent no-op). Fixed by dropping `-quit` — `-runTests` manages its own
   exit.
2. First full-suite attempt joined the 54-test `-testFilter` list with commas —
   Unity's separator is **`;`** — which silently matched **zero** tests
   (`Logs/p4_split_nographics.xml`, exit 0, "No tests were executed"). Fixed by
   rejoining with semicolons (`Logs/p4_split_nographics_v2.xml` then correctly
   ran 54 tests, surfacing pitfall 3 below).
3. `AnimalSpawner._spawned` is a static list; my new test's teardown destroyed
   the spawned GameObjects but never cleared the list itself, so 5 stale entries
   leaked into `ToolAnimalTests.Animals_SpawnBuyProduceCollect`'s
   `AnimalSpawner.Spawned.Count` assertion (expected 1, got 6 — one real failure
   in `p4_split_nographics_v2.xml`, not a false pass). Fixed by having the new
   test's teardown call `AnimalSpawner.SpawnOwned` with an empty-animals state,
   which both destroys and clears the static list — same reset path production
   code already uses when a save reloads.

**Phase 4: COMPLETE.**

---

## Phase-2 Spec / Phase 3 — collapsed phase: q_comet_chaser + q_festival_circuit (2026-07-05 ~17:45)

Both builds follow the user-approved rulings verbatim (QUEST_GAP_REPORT.md /
CLAUDE.md "Quest stub rulings") — no new systems, no new events, no data changes.

- **witness_event** (q_comet_chaser ×5): the existing `GameEvents.OnMeteorShower`
  subscription in `QuestEngine.Bind` now also runs
  `Bump("witness_event", s => s.@event == "meteor_shower")` alongside the
  Phase-2 `wait_for` completion — witnessing = counting firings of the same
  `MeteorShowerService` nightfall event. Progress rides the save-backed
  `questStepProgress` counter.
- **host_festival** (q_festival_circuit ×4): **SIMPLIFIED** per q_festival's own
  data precedent (its steps express festival-running as `harvest_any` +
  `talk_to` 4 NPCs). New `QuestEngine.HandleFestivalTalk` on the existing
  `OnTalkedTo` event: talking to **4 distinct NPCs of one Sacred Garden within
  a single in-game day** (`TimeManager.DayNumber`; a day rollover restarts
  partial tallies) hosts that garden's festival; `step.count` (4) distinct
  gardens complete the step, reusing the `_distinct` set + progress shape of
  `BumpDistinct`.
  - **DECISION (mechanical):** Sacred Gardens = the four **act-3 regions**
    (sun/moon/star/nebula_garden) — matched from `regions.json` by `act == 3`,
    no hardcoded id list. Per-garden talk quota = **4**, mirroring q_festival's
    own `talk_to` list of 4 NPCs. The `host_festival` step data carries no
    harvest quota, so none is imposed (the ruling's "+ harvest quota if the
    data specifies it" branch is vacuous here).
  - Same known limitation as `talk_to`: the in-memory tally/hosted-set resets on
    reload (existing precedent, not new behavior; hosted-garden *count* persists
    in `questStepProgress` but the garden identities don't).
- `PendingSystemSteps` shrinks to `challenge_run` + `trade_visitors`
  (q_challenge_sprout / q_galactic_traders — LEAVE STUBBED per ruling); the
  quest-log skip affordance disappears for the two built types automatically,
  and `QuestLogUI` renders their `[x/5]` / `[x/4]` progress generically with
  no UI changes.

**Verification evidence:**
- Compile: `Logs/phase3_compile.log` — 0 `error CS` lines.
- New tests (`Assets/Tests/PlayMode/QuestGapPhase3Tests.cs`, 6 cases):
  **6/6 PASS**, `Logs/test_results_p3phase3a.xml`, isolated via
  `-testFilter "CosmicBloom.Tests.QuestGapPhase3Tests"`. Coverage: 5 direct
  event firings complete q_comet_chaser (+1400c reward asserted); 5 **real
  `TimeManager` nightfalls** drive the full
  TimeManager→MeteorShowerService→GameEvents→QuestEngine pipeline end-to-end;
  4-garden circuit completes q_festival_circuit (festival_master_banner in
  `cosmeticsOwned` asserted); same-garden repeats don't double-count;
  starlight_vale (act-1) NPCs don't count; day rollover restarts a 3-of-4
  partial tally, then a full 4-NPC circuit within the new day hosts.
  (First `-runTests` attempt exited rc=1 in ~1s right after licensing logged
  "Access token is unavailable" — `Logs/phase3_tests.log`; transient, the
  identical rerun proceeded normally. Also: `Logs/test_results_p3.xml` was a
  **stale 7/4 file from an earlier session** that a first look misread as this
  run's output — deleted; the timestamped `*_p3phase3a` names avoid the trap.)
- Full-suite regression (same split as Phase 2):
  - Non-render half (`-nographics`), Phase-2 filter list + the new class —
    **53/53 PASS** (47 prior + 6 new), `Logs/p3_split_nographics.xml`.
  - Render-requiring half (5 screenshot tests, no `-nographics`, using the
    corrected `MobileScreenTests.CaptureTallPhoneHud` name so all 5 fit one
    invocation): **5/5 PASS**, `Logs/p3_split_render.xml`.
  - **Total: 58/58 PASS, 0 failures** — nothing in the QuestEngine changes
    broke any pre-existing test, and all 6 new Phase 3 tests are included.

**Phase-2 Spec / Phase 3: COMPLETE.** Remaining stubbed quests (by ruling, not
omission): q_challenge_sprout, q_galactic_traders.

---

## Phase-2 Spec / Phase 2 — quest mechanisms built (2026-07-05 ~12:15)

**Built** (all reuse existing verbs/events, no new architecture — see
`QUEST_GAP_REPORT.md` for the per-quest rationale):
- **catch_fish** (q_first_catch): `LandmarkController` keys off a restored
  landmark's own `unlocks=="fishing"` field (moonlit_pond) — tap-to-catch with
  an owned fish-action tool, small coin value, fires `GameEvents.FishCaught`.
- **collect** (q_journals, q_lost_seed, q_crystal_depths, q_star_charts) —
  real world pickups. New `World.PickupController` (tap → `ItemGranted`) +
  `World.PickupSpawner`, a reconcile loop keyed by (questId, stepIndex,
  itemIndex) so resumed saves respawn exactly the remaining items at the same
  deterministic spots. `QuestEngine.IsAnimalProduce` made public so the
  spawner and `IsPendingSystemStep` share one animal-produce check.
- **explore** (q_lost_seed, q_crystal_depths, q_meteor_mystery) — proximity
  poll in `QuestEngine.PollThresholdSteps`, same shape as the existing
  `move_to` case. **DECISION**: crystal_cave, crystal_cave_depths, and
  crash_site don't exist in `regions.json` (verified — no landmark ids
  match), so `World.ExploreTargets` is a small code-side target-position
  table (offsets from `starlight_vale`'s bounds center, the home region of
  every Act-2 giver): crystal_cave (8,-3), crystal_cave_depths (8,-6),
  crash_site (-8,-3). `World.ExploreMarkerController`/`ExploreMarkerSpawner`
  give it a visible pulsing beacon.
- **wait_for** (q_meteor_mystery) — new `Core.MeteorShowerService` fires
  `GameEvents.MeteorShower` on every `TimeManager.OnNightfall` (a full
  procedural-event system is out of scope; this only needs the event to occur
  once, observably, per the gap report). q_comet_chaser's witness_event count
  is Phase 3 (collapsed), not built here.
- **deposit_seeds** (q_seed_vault) — `LandmarkController` keys off
  `unlocks=="lore_journal_2"` (the arboretum) → `QuestEngine.TryDepositSeeds`
  consumes `count` seeds spread across `kinds` distinct owned crop types.
- **deliver** (q_pebbles_past) — new `SaveSnapshot.heldItems` field (additive,
  checked against `server/routes/game.js#validateSnapshot` — only
  schema/coins/level/farms/size are validated, so this is safe). Quest
  activation grants the item the moment a `deliver` step becomes current;
  talking to `step.target` while holding it completes + consumes it.
- **craft** (q_lantern_light) — **SIMPLIFIED**, user-approved: no recipe
  system. A one-entry code-side `CraftInputs` table in `QuestEngine` maps
  `festival_lantern <- star_honey`; each `ItemGranted(star_honey)` (existing
  bee-produce collection) directly bumps the craft step's progress.
- **q_toolsmith fix** (user-approved 2026-07-05): `ToolService.EnsureTools`
  extends the seed auto-buy pattern (`CropSelection.EnsureSeed`) to tools —
  called from `ToolService.Perform` (the action-execution path), *not* from
  any UI-query path (the Phase-9 crop-label bug was exactly this mistake for
  seeds). Any tool for the action being performed that the player's level
  qualifies for and can afford gets bought automatically. Covers all 5
  previously-unreachable tools (cosmic_hoe_2/3, solar_watering_can,
  nebula_mister, star_scythe, alien_harvester).
- **q_eternal_bloom fix** (user-approved 2026-07-05): `quests_completed` moved
  from an incremental `Bump()` (needed 59 MORE completions after activation —
  impossible with 60 total quests) to a threshold poll in
  `PollThresholdSteps` against `Save.questsCompleted.Count`, matching the
  `own_tools`/`own_animals` pattern.

**PendingSystemSteps** (still stubbed, unchanged from ruling): `witness_event`,
`host_festival`, `challenge_run`, `trade_visitors` — all optional Act-4 side
quests, none block the 47-quest chain. `collect`'s old "non-animal item is
pending" special case is removed (real pickups now exist for it).

**Verification evidence:**
- Compile: `Logs/phase2_compile.log` — 0 `error CS` lines.
- New tests (`Assets/Tests/PlayMode/QuestGapPhase2Tests.cs`, 13 cases, one per
  mechanism + failure-path cases for fishing-without-rod, deposit-seeds
  insufficient-variety, and deliver-wrong-target): **13/13 PASS**,
  `Logs/test_results_p2phase2d.xml`, isolated via
  `-testFilter "CosmicBloom.Tests.QuestGapPhase2Tests"`. (First isolated run,
  `test_results_p2phase2c.xml`, was 12/13 — caught a real bug in the test
  itself: `Object.Destroy` defers to end-of-frame, so the pickup-despawn
  assertion needed a `yield return null` before checking; not a production
  bug, fixed in the test.)
- **Full-suite regression investigation (2026-07-05, later same day):** three
  separate attempts to run the complete PlayMode suite hit native Unity
  crashes, each a different signature: (1) a `Camera.Render()` crash inside
  `CoreUiTests.CaptureEachUiScreen_Portrait` (URP `ScriptableRenderContext`),
  (2) a hang after crash during Editor shutdown inside
  `WorkerManagerASIO`/`TcpMessagingServerClientSession` teardown (process
  stayed alive post-crash-trace and had to be force-killed — confirmed hung,
  not slow, via `tasklist`), (3) a crash inside
  `DelayedCallManager::Update → Coroutine::ContinueCoroutine →
  mono_runtime_invoke`, reproduced twice. A `-testFilter` attempt to run the
  pre-existing classes as a comma-separated list also produced a spurious
  `test_results_p2full.xml` with `testcasecount="0"` — that was a
  filter-syntax miss (Unity's `-testFilter` didn't accept a multi-class comma
  list; `;` is the correct separator, confirmed later), not a passing
  regression check.
- **MCP relay investigated as a cause of (2)/(3) — dead end, reverted.** The
  AI Assistant package's MCP relay (`UserSettings/mcp.json`) auto-starts on
  every Editor domain load, batchmode included, and its own source comment
  (`RelayService.SuppressAutoStart`, package `com.unity.ai.assistant`)
  literally describes "a relay crash can otherwise poison strict LogAssert
  checks in any concurrently running test" — a strong textual match, but
  that method is `internal`, not actually callable from project code despite
  the doc comment. Set `mcp.json`'s `"enabled"` to `false` as a coarser
  attempt; a `-runTests` run afterward **still crashed at the identical
  `DelayedCallManager`/`Coroutine` stack, and `[relay]` log lines still
  appeared** — the flag does not stop the relay's in-Editor timer/coroutine
  machinery. Reverted `mcp.json` back to `"enabled": true`; **this crash
  signature (3) and the shutdown hang (2) remain unresolved** — not fixed by
  anything in this session, not re-diagnosed further, still open.
- **Root cause of (1) confirmed via clean-reboot repro:** with nothing else
  running and the active build target switched back to Standalone (see
  below), `CoreUiTests.CaptureEachUiScreen_Portrait` crashed again on a
  clean machine with the specific native error **"Unable to find surface for
  attachment 0"** — `-nographics` genuinely has no render surface, and
  `Camera.Render()` cannot work under it. This rules out RAM pressure and the
  Android build target as causes **of this specific crash** (they were not
  ruled out for (2)/(3), which are a different stack entirely). Along the
  way: switched the active build target from Android back to Standalone
  (Android became active during Phase 0 today; the last known-clean 39/39
  run, per the Phase 11 log, predates that switch) — kept at Standalone.
  Sentis (`com.unity.ai.inference`) was checked and is an independent
  top-level dependency in `Packages/manifest.json`, not pulled in
  transitively by `com.unity.ai.assistant` — that specific causal link for
  (2)/(3) doesn't hold, though Sentis isn't otherwise ruled out.
- **Fix implemented: split the PlayMode suite by render requirement.** Five
  tests actually call `Camera.Render()`/`ReadPixels` for screenshot capture —
  four are `Capture*`-named, plus one that isn't
  (`DayNightTests.LightingShift_NoonVersusMidnight_Screenshots`, same
  pattern, caught by grepping for `.Render()`/`RenderTexture` rather than
  trusting the naming convention):
  `CoreUiTests.CaptureEachUiScreen_Portrait`,
  `DayNightTests.LightingShift_NoonVersusMidnight_Screenshots`,
  `MobileScreenTests.CaptureTallPhoneHud` (note: lives in `MobileScreenTests`,
  a second class in `MobileInputTests.cs` — not in `MobileInputTests` itself,
  a naming trap worth remembering for future filter use),
  `RegionWorldTests.CaptureAllRegionsOverviewPng`,
  `ToolAnimalTests.CaptureAnimalInSceneScreenshot`. These five now run in
  their own batchmode invocation **without** `-nographics` (a real render
  surface); everything else keeps running with `-nographics` as before.
- **Real, final pass/fail numbers (2026-07-05, post-fix):**
  - Non-render half (47 tests, `-nographics`): **47/47 PASS**,
    `Logs/p2_split_nographics.xml`.
  - Render-requiring half (5 tests, no `-nographics`): **5/5 PASS**,
    `Logs/p2_split_render.xml` (4 of the 5) +
    `Logs/p2_mobilescreen_solo.xml` (the 5th, `CaptureTallPhoneHud`, run
    solo after the class-name correction above).
  - **Total: 52/52 PASS, 0 failures** — matches the suite's full
    `testcasecount`. This is the actual, conclusive full-suite regression
    result: nothing in `QuestEngine.cs`/`GameEvents.cs`/`ToolService.cs`/
    `LandmarkController.cs`/`GameBootstrap.cs`/`GameState.cs`/
    `QuestLogUI.cs` broke any pre-existing Phase 5–11 test, and all 13 new
    Phase 2 tests are included in the 47.

**STILL OPEN — do not treat as resolved:** crash signatures (2) (shutdown
hang in `WorkerManagerASIO`) and (3) (`DelayedCallManager`/`Coroutine`
crash) are different from the screenshot/render crash and were **not** fixed
by the `-nographics` split. They may be RAM-related, platform-related, or
something else entirely — genuinely undiagnosed. If either recurs, treat it
as a fresh investigation, not an instance of the render-surface issue.

**Phase-2 Spec / Phase 2: quest mechanisms COMPLETE, full regression
verification COMPLETE (52/52 pass). Two unrelated crash signatures from
today's investigation remain unresolved and open for a future session.**

---

## Phase-2 Spec / Phase 0 — COMPLETE ✅ (2026-07-05 ~10:40)

**Smoke-test verdict: the game runs on Android.** Non-development IL2CPP APK
(`Builds/Android/CosmicBloom-release.apk`, build log `phase0_android_build3.log`,
result=Succeeded, 0 errors) on the x86_64 emulator (ARM64 via binary translation):

```
[GameDatabase] 5 regions, 26 crops, 50 NPCs, 60 quests, 15 animals, 10 tools   ← LoadAll under IL2CPP ✓
[NpcSpawner] spawned 50 NPCs across 5 regions
[LandmarkSpawner] spawned 7 landmarks
[Quest] started q_arrival: The Arrival
--- injected REAL touch on the dialogue panel ---
[Quest] q_arrival → step 1 (move_to)
[Quest] COMPLETED q_arrival: The Arrival (+0c, +10xp)
[Quest] started q_first_sprout: First Sprout
process alive before/after all interactions (pid 12016) — zero crashes
```
Screenshots: `Logs/phase0_device_screenshot2.png` (HUD live, dialogue showing).
**The spec's #1 risk — Newtonsoft/IL2CPP reflection failure — did not materialize;**
no link.xml or AOT workarounds were needed.

**Findings for the record:**
1. Dev builds SIGILL in `profiling::Profiler::Initialize` under the emulator's
   ARM→x86 translation (engine-native, pre-managed-code). Unity 6000.5 REMOVED
   Android x86_64, so native emulator execution is impossible; the non-dev build
   sidesteps the profiler path and runs fine. On real ARM64 hardware dev builds
   are expected to work — verify when a device is available. Profiling sessions
   need real hardware regardless.
2. World viewport renders black on the emulator (SwiftShader GPU) while all UI
   renders correctly — suspect URP 2D-renderer light pass under emulated GL.
   Needs one look on physical hardware; UI/logic/data fully alive regardless.
3. Bundle id is still the URP template default — store-readiness item, out of scope.
4. Emulator infra installed from scratch into Unity's bundled SDK (emulator +
   android-34 x86_64 image + Pixel 5 AVD `CosmicBloomTest`), reusable for future runs.

### (earlier same-phase notes below)
## Phase-2 Spec / Phase 0 — Mobile build & runtime smoke test (2026-07-05, in progress)

**Findings so far:**
1. **Scripting backend: already IL2CPP for Android** (`scriptingBackend: Android: 1`
   in ProjectSettings) — no Mono finding, no switch needed.
2. Android module (SDK+NDK+JDK) already installed for 6000.5.1f1 — no Hub install needed.
3. No physical device attached and no Android Studio SDK existed. Installed the
   emulator + `system-images;android-34;google_apis;x86_64` **into Unity's bundled
   SDK via its own sdkmanager**; created a Pixel 5 AVD (`CosmicBloomTest`); boots
   headless with swiftshader.
4. **First APK build (ARM64-only, dev, IL2CPP): SUCCEEDED** — 0 errors, 57 MB APK
   (`Logs/phase0_android_build.log`, `[Phase0-Android] result=Succeeded`).
   IL2CPP itself compiles the whole project cleanly.
5. Bundle id is still the URP template default
   (`com.UnityTechnologies.com.unity.template.urpblank`) — cosmetic until store
   work (out of scope), but it initially misdirected the launch command; logged
   as a store-readiness item.
6. **CRASH FINDING:** the ARM64-only APK dies at engine init on the x86_64
   emulator — SIGILL in `libunity.so` `profiling::Profiler::Initialize` →
   `Mutex::AutoLock`, i.e. **inside Unity's native engine startup, before any
   managed/game code runs**. Not a Newtonsoft/JSON/IL2CPP-stripping failure —
   the managed layer never executed. Root cause assessment: Android's ARM→x86
   binary-translation layer choking on Unity's engine init, a known-flaky
   combination. **Fix applied (technical call, logged): dev builds now target
   ARM64 + x86_64** so the emulator executes natively; rebuild in progress.
   Caveat flagged honestly: a translation-layer crash can't 100% rule out a
   real-ARM64-device issue from an emulator alone — needs one run on physical
   hardware eventually (no device available tonight).


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

## Phase 2 — All five region tilemaps (2026-07-04 ~23:45)

**Built:**
- `World/RegionWorldBuilder.cs` — builds every region's Grid+Tilemap at runtime
  from `regions.json`: 20×20 cells, tiered rings (outer wild ring tier 0, mid ring
  tier 1, inner 8×8 lush core tier 2 = the future farmable area), colors strictly
  from each region's `groundTiers` (zero hardcoded colors). Regions sit 40 world
  units apart on X: vale@0, sun@40, moon@80, star@120, nebula@160.
- **DECISION:** region tilemaps are runtime-generated (extending Phase 1's bootstrap
  decision), not baked into the scene. The hand-baked StarlightVale grid was removed
  from the scene via `OvernightTasks.Phase2RemoveBakedGrid` — the builder recreates it
  with identical geometry (lush core still contains the existing save's plot at 10,10,
  asserted in tests). Scene YAML shrank 4931 → 576 lines. JSON color edits now show up
  without any rebake.
- The lone FarmPlot prefab instance currently idles at (0.5,0.5) — its grid-snap
  target no longer exists at Awake time. Replaced next phase by the farm grid.

**Verification evidence** (`Logs/test_results_p2.xml`, `Logs/phase2_regions.png`):
```
total: 7 passed: 7 failed: 0 (4 Phase-1 movement tests still green)
[P2Test] per-region tier colors asserted == regions.json for all 5 regions
[P2Test] bounds: vale(0..20) sun(40..60) moon(80..100) star(120..140) nebula(160..180) — pairwise non-overlap asserted
```
- **Screenshot captured headlessly** (`Logs/phase2_regions.png`, via camera→RenderTexture
  in the PlayMode run *without* `-nographics`): visually confirmed 5 distinct regions,
  each showing its 3-tier ring in the correct regions.json palette. The batchmode
  screenshot pipeline works — later phases get real visual evidence too.

**Phase 2: COMPLETE ✅**

---

## Phase 3 — Generic farming grid (2026-07-05 ~00:15)

**Built:**
- `World/FarmGrid.cs` — one per region (bootstrap creates all 5): spawns a
  `FarmPlot` view for every lush-core cell (8×8 = 64/region, 320 total), all
  backed by the save's per-region `FarmState.plots` via `FarmingSystem` —
  single data structure, no parallel state.
- `Farming/FarmPlot.cs` — rewritten generic: `(regionId, x, y)` + `IInteractable`.
  Tap runs the plot's contextual action (till→plant→water→harvest). Interaction
  requires being in the plot's region. Old GameObject.Find snap logic gone;
  prefab + lone scene instance deleted (`OvernightTasks.Phase3RemoveLonePlot`).
- `World/CropSelection.cs` — per-region selected crop (defaults to region's first
  purchasable crop). **DECISION:** quests award no seed items and no shop exists in
  scope, so planting with an empty pouch auto-buys one seed with coins at the
  data's `seedPrice` (all crops sell above seed cost except `clover_cover` — a
  deliberate soil investment — and `heartseed`, quest-only 0/0, excluded from
  picker/purchase). New saves get 5 starter `starlight_berry` seeds.
- `Core/GameEvents.cs` — central gameplay event hub (farming publishes
  tilled/planted/harvested/restoration; quest engine consumes in Phase 5).
- `FarmingSystem` deltas: **region-appropriateness enforced** (`def.region !=
  currentRegion → plant refused`), `GameState.FarmFor(regionId)` extracted,
  GameEvents published. `GameBootstrap` wires save→grids, region-switch
  FastForward, a 2s growth tick, and starter seeds.

**Verification evidence** (`Logs/test_results_p3.xml`, `Logs/test_run_p3.log`):
```
total: 11 passed: 11 failed: 0 (all prior phases still green)
[P3Test] starlight_vale(7,7) planted starlight_berry → harvested: coins 495→507
[P3Test] sun_garden(8,8)   planted solar_pepper    → harvested: coins 487→535
[P3Test] full loop OK: coins=12 xp=5 restoration=4.5 (native ×1.5 bonus asserted)
[P3Test] region gate OK: solar_pepper refused in starlight_vale, accepted in sun_garden
```
Spec's Phase-3 bar met with real interaction: **2 different plots, 2 different
regions, 2 different crops**, driven through `PlayerController.OnTapWorld` →
plot collider → contextual action, not debug keys. Watering-gates-every-stage
asserted separately.

**Phase 3: COMPLETE ✅**

---

## Phase 4 — Generic NPC system + dialogue (2026-07-05 ~00:50)

**Built:**
- `World/NpcController.cs` — ONE controller for all 50 NPCs; name/region/palette/
  day-night presence/dialogue all from `GameDatabase.Npc`. Placeholder capsule body
  tinted `palette.body` + accent chip tinted `palette.accent`. `activeAt` honored:
  day NPCs hide at night and vice versa (renderers+colliders toggle on
  sunrise/nightfall events). Tap → dialogue; intro on first meeting, idle after
  (met-state persisted in new `npcsMet` save field — validator-safe extension).
  Talking fires `GameEvents.TalkedTo` for the Phase 5 quest engine.
- `World/NpcSpawner.cs` — all 50 spawn into their data region, evenly ringed.
- `UI/UIRoot.cs` — code-built canvas (portrait 1080×1920 reference) + EventSystem
  with `InputSystemUIInputModule` (input-system-only project). All later UI hangs here.
- `UI/DialogueUI.cs` — bottom dialogue box: speaker, line, tap-to-close.
  Checked the data first: dialogue keys are only intro/idle/quest_complete —
  **no branching exists**, so linear display is correct, not a simplification.
- `TimeManager.OnDestroy` now clears its singleton (test hygiene).

**Verification evidence** (`Logs/test_results_p4.xml`):
```
total: 14 passed: 14 failed: 0 (all prior phases green)
[NpcSpawner] spawned 50 NPCs across 5 regions (10/region asserted)
[P4Test] zyx said:    "Oh! A new gardener! The Vale picked a good one…"   (starlight_vale)
[P4Test] lumina said: "Oh — hello. Sorry, I was talking to the ferns…"     (starlight_vale)
[P4Test] solmara said:"The forge went cold the year the stars dimmed…"     (sun_garden)
[P4Test] zyx 2nd chat → idle line (met-tracking verified)
night-only NPC hidden at noon / visible at night; day NPC inverse — asserted
```
3 NPCs across 2 regions each spoke their own data dialogue (spec bar: ≥3 NPCs,
≥2 regions ✓), plus per-NPC palette spawning for all 50.

**Phase 4: COMPLETE ✅**

---

## Phase 5 — Generic quest engine (2026-07-05 ~01:40)

**Built:**
- `Core/QuestEngine.cs` — ONE engine for all 60 quests; structure/triggers/rewards
  exclusively from quests.json. Event-driven step evaluators (via GameEvents) for
  clear_plots / plant_crop / harvest_crop / harvest_any / harvest_at_night /
  sell_any / sell_distinct / talk_to / restore_landmark / unlock_region /
  buy_animal / collect(animal produce), plus 1 Hz polling for threshold steps
  (soil_above, restoration_above, own_animals, own_tools, move_to). Cutscene steps
  present as dialogue and complete on dismiss.
- **DECISION — activation model:** `q_arrival` auto-starts on a fresh save; a
  completed quest auto-activates its `unlocks` successor (the 47-quest chain);
  side quests (no inbound `unlocks`) activate on first chat with their giver NPC.
- **DECISION — sale steps:** `FarmingSystem` sells on harvest (web-game behavior,
  no market UI in scope), so `sell_any`/`sell_distinct` count harvests.
- **DECISION — out-of-scope-system steps** (catch_fish, craft, explore, wait_for,
  witness_event, host_festival, challenge_run, trade_visitors, deposit_seeds,
  deliver, collect of non-animal items): the quest log surfaces an explicit
  "Skip step (system pending — overnight stub)" button instead of silently
  auto-completing. Every skip logs a warning. The 47-quest chain includes a few
  of these (first at quest #7, `q_first_catch`/fishing) — morning review decides
  whether to build those systems or keep the stubs.
- **DECISION — reward items routing:** tool ids → `tools`, `pet_*`/animal ids →
  `animals`, crop ids → `seeds`, everything else (trophies/story items like
  `celestial_atlas`, `legacy_mode`) → `cosmeticsOwned` (existing snapshot list).
- `UI/QuestLogUI.cs` — active quests with per-step progress + completed count
  (floating "Quests" button until the Phase 9 HUD).
- `World/LandmarkController.cs` + `LandmarkSpawner.cs` — all 17 landmarks from
  regions.json spawn as interactables; restoring costs `restoreCost` coins,
  persists in `FarmState.landmarksRestored`, feeds restore_landmark steps.
- Save fields added (validator-safe): `activeQuests`, `questStepIndex`,
  `questStepProgress` (+ `npcsMet` from Phase 4).

**Verification evidence** (`Logs/test_results_p5b.xml`):
```
total: 17 passed: 17 failed: 0
[Quest] started q_arrival → cutscene shown → move_to completed by walking to the core
[Quest] COMPLETED q_arrival (+0c, +10xp, tools granted per data)
[Quest] q_first_sprout: clear_plots(3)→plant_crop(5)→harvest_crop(1) all via real farming
[Quest] COMPLETED q_first_sprout (+50c, +25xp, axe granted)
[Quest] q_soil_secrets: plant 2 clover_cover + soil_above(60)×2 poll
[P5Test] chain state: completed=[q_arrival,q_first_sprout,q_soil_secrets] active=[q_green_thumb]
[P5Test] side quest q_master_gardener activated via lumina
```
**Three sequential main-chain quests completed via real triggers, each unlocking
the next** (spec bar: ≥3 ✓). Progress state survives a JSON snapshot roundtrip
(asserted). Rewards match quests.json exactly.

**Phase 5: COMPLETE ✅**

---

## Phase 6 — Animals & tools (2026-07-05 ~02:20)

**Built:**
- `World/AnimalController.cs` — ONE behavior for all 15 animals (palette tint,
  produce type/timer from animals.json). Wanders near home; tap = pet, or collect
  ready produce (glow cue). **DECISION:** produce has no price data → collecting
  grants `max(2, cost/40)` coins and fires `ItemGranted(produce)` which `collect`
  quest steps consume (q_egg_basket's glowing_egg ×5 works end-to-end).
- `World/AnimalSpawner.cs` — owned animals spawn in a Starlight Vale paddock
  (animals.json has no home-region field — flagged); `TryBuy` enforces data
  cost + unlockLevel and fires `AnimalBought` for buy_animal steps.
- `Farming/ToolService.cs` — tool tiers from tools.json shape farming areas.
  **DECISION — notes→area interpretation:** till t3 = 3×1 row; water t2 = target+
  4 neighbors, t3 = 3×3; harvest t1 = 1×2, t2 = 3×3; clear/fish don't map to plot
  actions this pass. Active tool = highest owned tier unless explicitly selected;
  selection persists in new `selectedTools` save field. FarmPlot taps now route
  through `ToolService.Perform`.
- Tool-switch UI: floating cycle button (Phase 9's HUD replaces it).

**Verification evidence** (`Logs/test_results_p6.xml`, `Logs/phase6_animals.png`):
```
total: 22 passed: 22 failed: 0
[P6Test] till: tier1=1 cell, tier3=3 cells — different behavior confirmed
tier-3 mister waters full 3x3 (9 plots) — asserted
[P6Test] chicken: buy(-200c per data) → pet (no produce) → ForceReady → collect:
         +5c, item=glowing_egg (quest event captured)
tool selection persists through snapshot roundtrip — asserted
```
- **Screenshot** `Logs/phase6_animals.png`: two tinted critters (Star Chicken cream,
  Star Sheep pale-blue) in the vale paddock — visually confirmed.
- Spec bar "two tool tiers showing different behavior": tier-1 hoe = 1 cell vs
  tier-3 hoe = 3 cells, plus 1 vs 9 cells for watering ✓.

**Phase 6: COMPLETE ✅**

---

## Phase 7 — Day/night cycle integration (2026-07-05 ~04:30)

**Built:**
- `World/DayNightLighting.cs` — global URP `Light2D` driven by
  `TimeManager.AmbientIntensity()`, tinted by the current region's
  `ambientTints` (dusk shoulder / night / deep-night blend); camera background
  follows region `skyColor` darkened at night. Created by GameBootstrap.
- World sprites switched from the unlit `FarmPlotSprite` material to a shared
  **Sprite-Lit-Default** material (`ProceduralSprites.LitMaterial`) — unlit
  sprites ignore Light2D, so this was required for any visible cycle.
- `CosmicBloom.Runtime.asmdef` gained `Unity.RenderPipelines.Universal.2D.Runtime`
  (Light2D/Renderer2DData live there in URP 17 — first run failed with CS0246,
  fixed, re-run green). `Assign2DRenderer` helper points cameras at the
  pipeline's Renderer2DData (the default renderer index is the non-2D one).
- Crop day/night gating: already implemented in `FarmingSystem.FastForward`
  (Phase 0 audit) — Phase 7 confirms it's ENFORCED, not just present in data.

**Verification evidence** (`Logs/test_results_p7.xml`, `Logs/phase7_noon.png`, `Logs/phase7_midnight.png`):
```
total: 26 passed: 26 failed: 0
[P7Test] wheat (growsAt=day): did NOT advance at night, grew at noon ✓
[P7Test] moon_melon (growsAt=night): did NOT advance at noon, grew at night ✓
[P7Test] ambient noon=1.00 midnight=0.18
[P7Test] rendered avg brightness: noon 0.403 vs midnight 0.055 (>1.5× asserted)
```
- **Screenshots**: same region captured at noon (fully lit, true ground colors)
  and midnight (near-black, faint tinted silhouettes) — visually confirmed the
  full cycle shift.

**Phase 7: COMPLETE ✅**

---

## Phase 8 — Restoration score & region unlock (2026-07-05 ~05:00)

**Built:**
- `World/RegionUnlockService.cs` — unlock + travel. Starlight Vale open from the
  start (snapshot default); the other four gated.
- **DECISION / spec divergence flagged:** the build spec assumed a direct
  restoration threshold gates region access, but the shipped data has no such
  field — regions.json gates unlocks on `unlockCoins` (1500/2500/4000/6000) +
  `unlockLevel` (8/12/16/20), and the quest chain interleaves
  `restoration_above` milestones (25/50/75/100) that supply the coins/xp,
  then `unlock_region` objectives. Implemented exactly what the data says;
  restoration drives unlocks *through the economy*, not as a direct latch.
  The spec's verify scenario was adapted accordingly (evidence below covers
  both halves of that loop).
- Restoration itself has accrued per-region since Phase 3
  (`FarmingSystem.Harvest`: `def.restore`, ×1.5 native bonus, clamped 0–100,
  persisted in the schema's existing `farms[region].restoration`). Nothing new
  needed — per-region tracking is native to schema 2 (Phase 0 finding).
- Note: regions.json `milestones` [25,50,75,100] have **no reward data anywhere**,
  so no claim mechanic was invented; `milestonesClaimed` stays as schema support.
- Travel confinement: movement clamps to the current region's bounds, so locked
  regions are physically unreachable (regions are 20 units apart; taps toward
  another region clamp at the edge — asserted).

**Verification evidence** (`Logs/test_results_p8.xml`):
```
total: 30 passed: 30 failed: 0
[P8Test] restoration artificially raised to 25 → restoration_above quest completed
         (the chain step that funds unlocks — spec's "raise restoration" half)
[P8Test] sun_garden unlock: level 7 refused, 1499c refused; at Lv8+1600c →
         coins 1600→100, unlockedRegions += sun_garden ("becomes accessible" half)
[P8Test] q_gates_of_sun (unlock_region step) completed via the real unlock event
[P8Test] travel: locked refused; unlocked → player at moon_garden center (90,10);
         tap toward the vale clamped to (80.30, 10) — confinement holds
```

**Phase 8: COMPLETE ✅**

---

## Phase 9 — Core UI (2026-07-05 ~06:10)

**Built (all code-generated uGUI on the Phase 4 canvas, portrait 1080×1920 reference):**
- `UI/HudUI.cs` — top stats bar (coins, level+XP, day/night + day number, current
  region + restoration %) and a bottom 5-button bar: Quests / Map / tool-cycle /
  crop-cycle / Menu. Replaces the Phase 5/6 floating buttons.
- `UI/RegionMapUI.cs` — Star Map: all five regions as region-tinted rows with
  live restored-% for unlocked ones and data-driven cost line for locked ones;
  tap to travel or attempt unlock (Phase 8 service).
- `UI/PauseMenuUI.cs` — pause (timeScale 0) / resume / manual save (writes via
  SaveManager) / Cosmic Ranch entry.
- `UI/RanchUI.cs` — all 15 animals listed from animals.json with cost + level
  + owned state; tap to buy.
- **Bug found by the tests and fixed:** the HUD's crop-label query called
  `CropSelection.SelectedFor`, which had the seed auto-purchase side effect —
  the UI was silently spending 5 coins per frame-1 label refresh. Split into
  pure `SelectedFor` (UI-safe) vs `SelectedForPlanting` (plant path only).
- **Batchmode lesson:** `WaitForEndOfFrame` never completes headlessly — the
  first UI capture run hung Unity and had to be killed. Replaced with
  frame-yields; all later capture tests inherit the fix.

**Verification evidence** (`Logs/test_results_p9b.xml`, `Logs/phase9_*.png`):
```
total: 33 passed: 33 failed: 0
[P9Test] HUD: ⛁ 321  Lv 1 (0/100xp)  ☀ Day 1  Starlight Vale: 0% restored (live state asserted)
quest log opens/lists The Arrival step 2/2; map lists all 5 regions with data costs;
pause sets timeScale 0 and back — all asserted
```
- **Screenshots (portrait 720×1280)**: `phase9_hud.png`, `phase9_questlog.png`,
  `phase9_map.png`, `phase9_dialogue.png`, `phase9_pause.png` — visually
  confirmed each screen renders with live data and no ruinous overlap.
  Morning-polish note: the dialogue box slightly overlaps the bottom button bar
  (dialogue is modal, so no interaction conflict, but it's visually untidy).

**Phase 9: COMPLETE ✅**

---

## Phase 10 — Save/load full coverage + backend validation (2026-07-05 ~06:50)

**Built:**
- Snapshot fields added (all validator-safe extras): `playerX/playerY/hasPlayerPos`
  (position restore), `timeOfDay/dayNumber` (world clock persistence) — on top of
  the phase-by-phase additions (`npcsMet`, `activeQuests`, `questStepIndex`,
  `questStepProgress`, `selectedTools`).
- `SaveManager.BeforeWrite` event: live systems stamp the snapshot at write time;
  `GameBootstrap` restores player position + clock on load and subscribes the stamper.
- `TimeManager.SetDayNumber` for clock restore.

**Verification evidence:**
1. **Cross-process reload** (the headless equivalent of "close and reopen the
   Editor"): writer test ran in one Unity process (`test_results_p10a2.xml` 1/1),
   reader in a **fresh process** (`test_results_p10b2.xml` 1/1):
   `[P10Test-B] every field verified after cross-process reload ✓` — coins,
   plots+crop+water state in two regions, restoration 33.5, landmark, quest
   progress (q_green_thumb @4), npcsMet, tools+selection, animals, player pos
   (12.25, 8.75), timeOfDay 0.42, dayNumber 3 — each asserted before/after.
2. **Real backend round-trip** — MongoDB isn't installed on this machine and no
   MONGODB_URI exists, so the server had never run here. Solution *without
   touching server code*: `mongodb-memory-server` (already one of the backend's
   own declared dependencies) on port 27777 + a temporary `.env`, then
   `node server/index.js`. Register (201) → `POST /api/game/save` with the real
   Unity-written snapshot → **200 "Game saved"** through the actual
   `validateSnapshot` → `GET /api/game/load` → **byte-identical diff, 0 mismatches**.
   Bonus finding: the server's last-write-wins correctly rejected a stale
   lower-`saveVersion` snapshot during rehearsal ("Stale save ignored").
   Cleanup done: server + memdb stopped, temp `.env` and launcher deleted —
   backend repo pristine.
3. Size headroom: full snapshot is ~1KB; worst case (all 320 designed plots
   with crops) lands well under both the 1000-plots/farm and 300KB caps.
4. **Definition-of-done playthrough** ran as a PlayMode test
   (`FullPlaythroughTests`, part of the 36/36 in `test_results_p10a.xml`):
   `[Playthrough] DONE — vale 27.0% | sun 4.5% | coins=148 level=8 quests=2
   unlocked=[starlight_vale,sun_garden]` — arrive → quest chain → farm →
   restoration ≥25 → unlock Sun Garden (paid, real gate) → travel → farm the
   sun-native crop there. (Coin/XP grind accelerated at quest scale; every
   mechanic in the loop is the real implementation.)

**Phase 10: COMPLETE ✅**

---

## Phase 11 — Mobile input & UI pass (2026-07-05 ~07:40)

**Built / changed:**
- **Real simulated-touch verification**: `MobileInputTests` uses
  `InputTestFixture` + a virtual `Touchscreen` — an actual screen-space touch
  tap drives movement and plot interaction through the full input pipeline
  (no `OnTapWorld` shortcut). Test asmdef gained `Unity.InputSystem` +
  `Unity.InputSystem.TestFramework` references.
- **Bug found & fixed by these tests:** `Pointer.press.wasPressedThisFrame`
  misses presses when editor and dynamic input updates interleave —
  PlayerController now does its own `isPressed` edge detection (identical
  behavior on device, robust everywhere).
- Portrait orientation set in ProjectSettings (`defaultScreenOrientation`
  AutoRotation→Portrait) per the GDD's portrait-mobile design.
- UI was already touch-native: same Pointer path for mouse/touch,
  `InputSystemUIInputModule` EventSystem, 1080×1920 reference scaler.

**Verification evidence** (`Logs/test_results_p11f.xml`, `Logs/phase11_tallphone.png`):
```
total: 39 passed: 39 failed: 0   (entire suite, all phases, final regression)
[P11Test] touch at screen (457,309) → move target (14.00,12.00) → arrived ✓
[P11Test] plot (7,7) tilled via touchscreen tap ✓
```
- **Screenshots at real mobile resolutions**: `phase11_tallphone.png`
  (1080×2340, 19.5:9) — HUD bars anchor correctly, large touch targets, no
  overlap; plus the five 720×1280 portrait screens from Phase 9.
- Pending human check (flagged, not blocking): a hands-on pass in Unity's
  Device Simulator window (GUI-only feature — can't run headless) to eyeball
  notch/safe-area handling.

**Phase 11: COMPLETE ✅**

---

## FINAL STATUS — all 11 phases complete (2026-07-05 ~07:45)

**Definition of done met:** the `FullPlaythroughTests.ValeToSunGarden_CoreLoop`
PlayMode test walks the exact required loop with real systems: start in
Starlight Vale → complete quests (q_arrival, q_first_sprout via actual
farming) → raise restoration to 27% → unlock Sun Garden (paying the
data-driven 1500c at Lv8) → travel there → till/plant/water/harvest a
sun-native crop. Final suite: **39/39 tests green**, one commit per phase
(11 commits + baseline), all evidence inline above and in `Logs/`.

**Morning review queue (all flagged inline above):**
1. DECISION: seed economy — auto-buy at `seedPrice` when pouch empty (no shop UI in scope).
2. DECISION: quest steps for unbuilt systems (9 of 47 chain quests, first at #7
   `q_first_catch`) show an explicit skip button — decide build-vs-keep per system.
3. DECISION: sale quest steps count harvests (instant-sell model, as web game).
4. DECISION: animal produce coin value `max(2, cost/40)`; animals home in the vale paddock.
5. DECISION: tool-tier area interpretations (till 3×1, water adjacent/3×3, harvest 1×2/3×3).
6. Spec divergence: region access = coins+level per data, not a direct restoration latch.
7. Dialogue box overlaps bottom bar visually (modal, cosmetic).
8. Unity Hub was force-killed twice after auto-relaunching the Editor (it held
   the project lock); restart the Hub normally when convenient.
9. Visual/device checks pending human eyes: device-simulator pass, in-editor feel
   of camera follow + day/night transition at real time scale.


The first Phase 7 test batch aborted at startup (return code 1): a GUI Unity
Editor had been **relaunched by Unity Hub** (same `hubSessionId` as the instance
closed at session start) and held `Temp/UnityLockfile`. Nobody is present to
close it, so I force-terminated the Editor (PIDs 19240/7400) and **also killed
Unity Hub** (4 processes) to stop it relaunching again overnight. Disk state was
fully committed to git at the time; the relaunched instance contained no work.
Subsequent batch runs confirm licensing still works without the Hub. If the Hub
is wanted in the morning, just start it normally.

---


