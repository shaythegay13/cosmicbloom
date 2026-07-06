# Cosmic Bloom — Project Context

## What this is
Unity 6 (6000.5.1f1) mobile farming game. Data-driven core loop: farm → rewild → unlock regions → progress through a 47-quest main chain across 4 acts. Pivoted from a finished web game ("Farm & Give," same repo under client/server/ios/android/tools) — that old code is reference only. Don't touch it or port from it directly.

## Locations
- Unity project: `D:\Unity\Cosmic Bloom`
- Editor binary: `D:\Unity\6000.5.2f1\6000.5.1f1\Editor\Unity.exe`
- Source repo (data + old web game): `D:\FarmandGiveGame`
- Backend (Node/Express + MongoDB, unchanged, don't touch): `D:\FarmandGiveGame\server`
- GDD: `COSMIC_BLOOM_GDD.md` in the source repo
- Build log: `BUILD_LOG.md` in the Unity project root — append, never recreate
- Quest gap report: `QUEST_GAP_REPORT.md`

## Architecture
- Namespace `CosmicBloom`, split by folder: Core, Data, Farming, Net, Save, Config, World, UI, Player.
- Everything dynamic (player, NPCs, animals, plots, UI) spawns at runtime from data via `GameBootstrap` — the scene only holds managers/camera/light/tilemap grids. Don't hand-place gameplay objects in the scene; add them to a spawner.
- `GameDatabase.LoadAll()` loads all 6 JSON files (regions/crops/npcs/quests/animals/tools) from `Assets/Resources/GameData/`. `.Region(id)` / `.Crop(id)` / `.Npc(id)` / `.Quest(id)` are the lookups.
- `FarmingSystem` owns till/plant/water/harvest + soil + day/night grow gating. `FarmGrid` instantiates `FarmPlot` views per region, backed by the save's `FarmState.plots` — one data structure, no parallel state.
- `QuestEngine` is generic — reads `quests.json`, no per-quest code, ever. `GameEvents` is the pub/sub hub connecting farming/NPC/quest systems.
- `SaveManager`: local-first, debounced, schema 2. Save fields are additive-only — new fields are fine, don't restructure existing ones without checking `server/routes/game.js#validateSnapshot` first.
- New Input System only (`activeInputHandler: 1`). Never use `UnityEngine.Input` — it throws.

## Verification discipline — do not skip this to save time
- No live Unity MCP dependency for verification. Headless batchmode for compiles:
  `"D:\Unity\6000.5.2f1\6000.5.1f1\Editor\Unity.exe" -batchmode -nographics -projectPath "D:\Unity\Cosmic Bloom" -quit -logFile <path>`
- Unity Test Runner PlayMode tests (headless `-runTests`) for behavior — not manual keypresses, not live Play-mode narration.
- Evidence means an actual file: test XML, a log, a screenshot. Never report something as working without citing the file that proves it.
- Commit to git after every completed unit of work.
- Content/design decisions (anything that changes story, quests, or what the game actually is) — stop and report, don't decide unilaterally, even unattended. Technical/mechanical judgment calls are fine to make and log.

## Decisions already made — don't relitigate these
- Seed economy: auto-buy at `seedPrice` when pouch is empty. No shop UI in scope.
- Sales: instant-sell on harvest, no market UI (matches the old web game).
- Region unlock: coins + level per `regions.json`, NOT a direct restoration threshold. Restoration feeds the economy through quest rewards, not a direct gate.
- Tool tiers: till t3=3×1, water t2=+4 neighbors/t3=3×3, harvest t1=1×2/t2=3×3.
- `GameConfig.CharityEnabled` stays `false`. Don't touch it.
- Animal region distribution (Phase 4, 2026-07-05): `animals.json` has no
  home-region field — `AnimalSpawner.HomeRegionByAnimal` is a code-side lookup
  table (same pattern as `ExploreTargets`), assigned from explicit landmark
  notes, exact palette hex matches to a region's `groundTiers`, then name/
  behavior theming, falling back to `starlight_vale`. 9 of 15 animals stay in
  the Vale (starter livestock, pollinator/helper roster, explicit Moonlit Pond
  ties); the rest split across the four Sacred Gardens. See BUILD_LOG.md Phase
  4 for the full per-animal rationale. Known gap, not fixed: buying an animal
  only checks `unlockLevel`/coins, not whether its home region is unlocked yet
  (worst case `nebula_cow` at level 9 vs. Nebula Garden's level-20 unlock) —
  harmless since all 5 regions are always built/registered, the animal just
  waits there.

### Quest stub rulings (user-approved 2026-07-05 — see QUEST_GAP_REPORT.md)
- BUILD (Phase 2): fishing (ToolService action at restored moonlit_pond), world
  pickups (collect), explore proximity markers, wait_for scheduled events
  (meteor_shower = shared infra, built ONCE), deposit_seeds (arboretum check),
  deliver (talk_to + item-held). Plus q_lantern_light's crafting SIMPLIFIED to
  star_honey→festival_lantern conversion (no recipe system) — Phase 2, not 3.
- Explore targets (crystal_cave, crystal_cave_depths, crash_site) don't exist in
  regions.json — a code-side target-position table is the approved approach; log
  its coordinates as a DECISION in BUILD_LOG when built.
- BUILT (collapsed Phase 3, 2026-07-05): q_comet_chaser — witness_event counts
  firings of the same meteor_shower event wait_for uses; q_festival_circuit —
  host_festival simplified per q_festival's own data pattern (4 distinct NPCs
  of one Sacred Garden — the four act-3 regions — within one in-game day = one
  festival; the step data carries no harvest quota, so none is imposed).
  LEAVE STUBBED: q_challenge_sprout, q_galactic_traders — challenge_run and
  trade_visitors are now the only PendingSystemSteps.
- Phase-2 fixes from the side-quest inventory: tool-shop affordance (5 tools have
  cost data but no acquisition path — q_toolsmith is un-completable without it);
  quests_completed evaluator must be a threshold poll, not an incremental counter
  (q_eternal_bloom impossible as-is).
- q_toolsmith fix (user-approved 2026-07-05): same pattern as the seed economy —
  extend the existing auto-buy-on-need logic to tools. No tool vendor, no shop UI.
- q_eternal_bloom fix (user-approved 2026-07-05): pure evaluator logic — poll
  total-quests-completed-to-date instead of counting incrementally from quest
  activation. No design input needed, mechanical fix only.

## Out of scope — don't build unless explicitly asked
Monetization/IAP, final commissioned art, store compliance, audio, cutscene polish beyond dialogue, multiplayer, localization.

## Token/session hygiene
- Don't run multiple phases of a multi-phase spec in one continuous session — each message resends the full history, so cost compounds fast on long sessions. Finish a phase, commit, and start a fresh session for the next one. `BUILD_LOG.md` and this file are the handoff — not the conversation transcript.
- For read-heavy investigation work (auditing files, evaluating whether something is reusable), delegate to a subagent so the main session's context doesn't fill up with file contents it doesn't need to keep.
- Default to Sonnet 5 for mechanical work. Escalate to Fable 5 only for genuine judgment calls (design tradeoffs, ambiguous requirements, debugging something that resists a first fix).
