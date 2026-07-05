# Quest Gap Report — Stubbed Step Types

Date: 2026-07-05. Scope: every quest currently showing the "[skip — system
pending]" affordance instead of real content. Sources: `quests.json`,
`regions.json`, `tools.json`, `COSMIC_BLOOM_GDD.md` — all quoted from the
actual data, not from memory.

**Headline finding:** the 9 affected main-chain quests are all Act 1–2; the four
"unclear" step types the spec asked about (witness_event, challenge_run,
trade_visitors, host_festival) turn out to live **only in Act-4 optional side
quests** (`unlocks: null`) — none of them block the 47-quest chain. Fixing the
chain requires exactly five mechanisms: fishing, collect-pickups, explore
markers, a wait/event timer, deposit/deliver — all cheap reuses of existing
systems.

---

## Main-chain quests (block progression past quest #7)

### 1. q_first_catch — "First Catch" (Act 1, chain #7, giver: juniper)
> "Juniper's stew needs fish. Catch 3 from the restored pond."
> step: `{"type": "catch_fish", "count": 3}`

- Data support is strong: `tools.json` has `fishing_rod` (action `fish`, tier 1,
  *"Quest reward after restoring the Moonlit Pond"*); the pond landmark's
  `unlocks` field is literally `"fishing"`; the preceding chain quest
  (q_moonlit_pond) awards the rod.
- **Recommendation: BUILD.** It's a `ToolService` action against the restored
  moonlit_pond landmark: tap the pond with the rod owned → timer → fish caught
  (+ small coin value, `CropHarvested`-style event for the counter). No new
  architecture. Confirms the spec's hypothesis.

### 2. q_journals — "Voices in the Dirt" (Act 2, chain #16, giver: elder_nova)
> "Find the first three journals of the Starlight Clan scattered across the Vale."
> step: `{"type": "collect", "item": "clan_journal", "count": 3}`

- GDD Act 2: *"Old journals scattered across the map reveal the Starlight
  Clan's downfall"* — the journals are a named lore device, but mechanically
  this is tap-to-pick-up.
- **Recommendation: BUILD** (world pickups). A spawnable `PickupController`
  (IInteractable, fires `GameEvents.ItemGranted`) placed at deterministic spots
  when the quest activates. Same evaluator already consumes ItemGranted.
  Narrative content (journal text) can be one dialogue line per pickup using the
  existing DialogueUI — no new UI.

### 3. q_lost_seed — "The Lost Seed" (Act 2, chain #17, giver: orbit)
> "A rare seed is hidden in the crystal cave. Orbit swears it isn't one of his rocks."
> steps: `explore crystal_cave` → `collect rare_seed ×1`

- **Note:** `crystal_cave` is NOT a landmark in `regions.json` (checked all 7
  landmark ids) — explore targets are new named locations that exist nowhere in
  data. They need world markers spawned by the quest system.
- **Recommendation: BUILD** (explore = proximity marker). An explore step
  spawns a marker at a deterministic spot in the quest giver's region; walking
  within range completes the step (same polling pattern as `move_to`, which is
  already implemented). The collect step then drops a pickup at the marker.
  Slightly more than "chained move_to" (the spec's hypothesis) because the
  targets don't exist in data — but only slightly: one marker component + a
  target-position table.

### 4. q_crystal_depths — "Crystal Depths" (Act 2, chain #18, giver: orbit)
> "The cave goes deeper than anyone thought. Light the way and map the lower gallery."
> steps: `explore crystal_cave_depths` → `collect prism_shard ×3`

- Same shape as q_lost_seed. **Recommendation: BUILD** with the same two pieces.

### 5. q_meteor_mystery — "The Meteor Mystery" (Act 2, chain #19, giver: orbit)
> "Strange lights over the ridge. Investigate the crash site during the night."
> steps: `wait_for meteor_shower` → `explore crash_site`

- GDD lists meteor showers under *"Procedural events: meteor showers, comet
  visits, alien festivals"* — a full event system is out of scope, but this
  step only needs the event to OCCUR once, observably.
- **Recommendation: BUILD** (minimal scheduled event). A `wait_for` evaluator
  that schedules the named event a short in-game time out (e.g. next nightfall
  via the existing `TimeManager.OnNightfall`), shows a dialogue line
  ("A meteor shower streaks overhead!"), completes the step. The explore step
  then reuses the Phase-2 marker. This is the spec's "waiting" case — cheap.

### 6. q_seed_vault — "The Seed Vault" (Act 2, chain #22, giver: lumina)
> "The arboretum hides a clan seed vault. Stock it with 3 different kinds of seeds."
> step: `{"type": "deposit_seeds", "kinds": 3, "count": 9}`

- The celestial_arboretum landmark exists and is restorable already.
- **Recommendation: BUILD** (thin evaluator, no new system): tapping the
  restored arboretum with ≥9 seeds across ≥3 crop kinds consumes them and
  completes the step. It's an inventory check + subtract on an existing
  interactable. (Seeds are purchasable, so this can't dead-end.)

### 7. q_pebbles_past — "Pebble's Past" (Act 2, chain #23, giver: captain_pebble)
> "Deliver a sealed crate to Elder Nova. Don't shake it. Don't ask."
> step: `{"type": "deliver", "item": "sealed_crate", "target": "elder_nova"}`

- Target is an NPC id — both NPCs exist with dialogue.
- **Recommendation: BUILD** (composition, not system): quest activation grants
  `sealed_crate` (item-held flag in cosmeticsOwned or a small heldItems list);
  the existing `TalkedTo(elder_nova)` event + item check completes it and
  removes the item. Confirms the spec's hypothesis (talk_to + item check).

### 8. q_star_charts — "Star Charts" (Act 2, chain #25, giver: captain_pebble)
> "Pebble's charts burned in the crash years ago. Recover 3 fragments from around the Vale."
> step: `{"type": "collect", "item": "chart_fragment", "count": 3}`

- **Recommendation: BUILD** — identical to q_journals (world pickups).

### 9. q_lantern_light — "Lantern Light" (Act 2, chain #27, giver: juniper)
> "String the market square with light: craft 10 festival lanterns from star honey and glass."
> step: `{"type": "craft", "item": "festival_lantern", "count": 10}`

- **No recipe data exists anywhere** — no ingredients file, no "glass" item in
  any JSON; `star_honey` exists only as bee produce. Building a crafting system
  for one step is disproportionate, confirming the spec's hypothesis.
- **Recommendation: SIMPLIFY.** The summary names the inputs: star honey (bee
  produce — the animal-produce system already generates it) and glass
  (nonexistent). Reframe as: *deliver/spend 10 star_honey at the market square*
  → each converts to a `festival_lantern` via `ItemGranted`. Mechanically it's
  "collect produce ×10 + one tap", using only existing verbs, and it keeps the
  fiction ("made from star honey"). Flagging honestly: bee produce timers make
  this a slow quest; suggest also counting coin purchase of lanterns at the
  market stand-in (data has `festival_lantern` as a reward item already, so the
  item id is established).

---

## Side quests (Act 4, optional, `unlocks: null` — nothing blocks on them)

### q_comet_chaser — "Comet Chaser" (giver: orbit) — `witness_event meteor_shower ×5`
> "Witness 5 meteor showers. Orbit will trade stories for every one."
- Once Phase 2's minimal scheduled meteor-shower event exists (needed for
  q_meteor_mystery anyway), witnessing = being present when it fires.
- **Recommendation: BUILD-BY-REUSE** — count firings of the same event. Nearly free.

### q_challenge_sprout — "Challenge: Bare Hands" (giver: cosmo) — `challenge_run no_upgrades ×20`
> "Cosmo's dare: harvest 20 crops on a fresh challenge farm with no tool upgrades."
- Requires a separate "challenge farm" save-scope and upgrade-lockout rules —
  a real mode, not a verb. GDD ties challenge runs to Act 4 "Legacy mode".
- **Recommendation: SIMPLIFY (conservatively)** to "harvest 20 crops using only
  tier-1 tools" tracked on the main farm (evaluator checks equipped tool tier at
  harvest), OR leave stubbed. It's optional endgame; either call is safe. Mild
  preference: simplify, since the tier check is one line in the harvest event.

### q_galactic_traders — "Galactic Traders" (giver: captain_pebble) — `trade_visitors ×5`
> "Word has spread. Trade with 5 different alien visitors from beyond the Vale."
- "Alien visitors" appear nowhere else in data — no visitor NPCs, no schedule.
  GDD mentions seed import/export with alien traders as Act-4 flavor.
- **Recommendation: SIMPLIFY** to "sell to Captain Pebble on 5 different
  in-game days" (sell events already exist; day counter exists), or leave
  stubbed. No strong data anchor either way; not worth new NPC infrastructure.

### q_festival_circuit — "Festival Circuit" (giver: juniper) — `host_festival ×4`
> "Host a seasonal festival in all four Sacred Gardens."
- Precedent in the data itself: the in-chain "Festival Preparation" quest
  (q_festival) expresses festival-running entirely through existing verbs
  (`harvest_any ×20` + `talk_to` 4 NPCs). The data's own festival treatment is
  compositional.
- **Recommendation: SIMPLIFY** following that exact precedent: "in each of the
  four gardens: talk to N of that region's NPCs (+ optionally harvest quota)
  within one in-game day" → one festival per garden. Uses NpcController +
  TalkedTo + DayNumber only.

---

## Narrative-centrality check (the Phase 3 exception clause)

None of the four simplify-candidates is a story capstone: the Act-2 finale is
q_whispers (cutscene, already works); the Act-3/4 capstones are heartseed /
restoration quests (already work). q_lantern_light feeds the festival arc but
its *content* (lanterns from star honey) is preserved by the simplification.
The four Act-4 side quests are explicitly optional endgame repeatables.
**No stop-condition triggered** — but per the spec, Phase 1 halts here for
review regardless.

## Addendum (2026-07-05, post-review) — inventory of the 9 remaining side quests

Per review instruction: verified each *completes via its real trigger*, not just
that its step type has an evaluator. Result: **7 of 9 genuinely completable,
2 broken in ways the first pass didn't catch:**

| Quest | Step | Verdict |
|---|---|---|
| q_master_gardener | harvest_any ×500 | ✅ completable (grindy by design) |
| q_menagerie | own_animals ×10 | ✅ completable (15 animals, RanchUI buys) |
| **q_toolsmith** | own_tools ×10 | ❌ **BROKEN — no acquisition path for 5 of 10 tools.** cosmic_hoe_2/3, nebula_mister, star_scythe, alien_harvester have `cost`+`unlockLevel` data but nothing sells tools (same gap class seeds/animals had — they got auto-buy/RanchUI; tools got nothing). Needs a tool-shop affordance in Phase 2. |
| q_seed_baron | sell_any ×1000 | ✅ completable (harvest-sale counter) |
| q_perfect_soil | soil_above 100 ×20 | ✅ completable (soil clamps at 100; fresh/fallow plots reach it) |
| q_night_owl | harvest_at_night ×50 | ✅ completable |
| q_legacy_begins | cutscene | ✅ completable |
| q_constellation_map | harvest_crop starfruit ×12 | ✅ completable (starfruit exists in crops.json, star_garden) |
| **q_eternal_bloom** | quests_completed ×59 | ❌ **BROKEN — evaluator-design bug, not data.** The engine counts completions *incrementally after activation*; needing 59 more post-activation completions out of 60 total is impossible. Must be a threshold poll against `questsCompleted.Count` (same pattern as own_tools). One-line fix, Phase 2. |

## Summary table

| Quest | Chain pos | Type | Recommendation |
|---|---|---|---|
| q_first_catch | #7 | catch_fish | **Build** — ToolService action at the pond |
| q_journals | #16 | collect | **Build** — world pickups |
| q_lost_seed | #17 | explore+collect | **Build** — proximity markers + pickups |
| q_crystal_depths | #18 | explore+collect | **Build** — same |
| q_meteor_mystery | #19 | wait_for+explore | **Build** — minimal scheduled event |
| q_seed_vault | #22 | deposit_seeds | **Build** — inventory check on arboretum |
| q_pebbles_past | #23 | deliver | **Build** — talk_to + item-held |
| q_star_charts | #25 | collect | **Build** — world pickups |
| q_lantern_light | #27 | craft | **Simplify** — star-honey conversion, no recipe system |
| q_comet_chaser | side | witness_event | Build-by-reuse (count event firings) |
| q_challenge_sprout | side | challenge_run | Simplify (tier-1-only harvests) or leave stubbed |
| q_galactic_traders | side | trade_visitors | Simplify (sell across 5 days) or leave stubbed |
| q_festival_circuit | side | host_festival | Simplify (per-garden talk_to+harvest, data's own precedent) |
