# Cosmic Bloom — Phase 2 Build Spec

Continuation of the overnight build. All 11 original phases are complete and verified (39/39 tests passing, confirmed independently against the raw XML). This spec covers what's actually next.

**Same working discipline as the previous spec: evidence over claims, headless batchmode + PlayMode tests over live MCP, `BUILD_LOG.md` gets new dated entries appended (don't start a new file), commit per phase.** Not re-explaining that ruleset here — refer back to it.

**One change from last time: Phase 1 is a hard stop.** Everything else can run autonomously, attended or not. Phase 1 cannot — it produces a report and waits for a decision, because it touches game content and story, not just code.

---

## Before you start — token efficiency

Running the whole first build cost real money, and most of the spend wasn't the actual work — it was session structure. Fix that before running Phase 0.

1. **Drop `CLAUDE.md` (provided separately) into the project root of `D:\Unity\Cosmic Bloom` before starting.** It has the architecture, file locations, and decisions already made. Every session reads it automatically at startup — this replaces re-deriving "what is this project" from scratch, which is what Phase 0's original audit had to do the hard way last time.

2. **Run each phase as its own session — don't chain all 8 phases in one continuous conversation.** Every message in a session resends the entire prior history as input tokens; a long single session compounds cost badly by the later phases. Finish a phase, commit, close the session. `BUILD_LOG.md` and `CLAUDE.md` are the handoff between phases, not conversation memory.

3. **Route by model, not uniformly on the most expensive one:**

   | Phase | Model | Why |
   |---|---|---|
   | 0 — Mobile build & smoke test | Sonnet 5 | Mechanical, until something actually breaks (IL2CPP failure) — escalate only that specific debugging if it happens. |
   | 1 — Quest investigation | Fable 5 | Real judgment calls on build-vs-simplify. Worth the reasoning depth. |
   | 2 — Build tractable systems | Sonnet 5 | Reusing established patterns, well-specified. |
   | 3 — Resolve/cut disproportionate systems | Fable 5 | Same judgment-call territory as Phase 1. |
   | 4 — Animal region distribution | Sonnet 5 | Small, mechanical, low ambiguity. |
   | 5 — Reuse art pipeline | Fable 5 to evaluate reusability, Sonnet 5 to wire it up once decided | The judgment call is expensive to get wrong; the mechanical wiring isn't. |
   | 6 — UI polish | Sonnet 5 | Layout fixes, low ambiguity. |
   | 7 — Full regression | Sonnet 5 | Running and reporting tests is mechanical; escalate only if something unexpected fails. |

   Switch models with Claude Code's `/model` command at the start of each session.

4. **Turn off auto-memory background calls** (`/memory` → disable, or set `DISABLE_NON_ESSENTIAL_MODEL_CALLS=1`). Claude Code forks a parallel API call after every message to extract memories for future sessions — that's a second, invisible input-token charge on every single turn, and `CLAUDE.md` now covers that job better anyway.

5. **What this doesn't change:** the evidence bar. Cutting verbose prose explanation is fine. Cutting test output, screenshots, or citing the actual file that proves something worked is not — that discipline is what made the last build log trustworthy, and it isn't the expensive part.

---

## Phase 0 — Mobile build & runtime smoke test

Nothing has been verified outside the Unity Editor. That's the biggest untested assumption in the whole project.

1. Check the project's current scripting backend (Edit → Project Settings → Player → Configuration → Scripting Backend). Report whether it's Mono or IL2CPP. Android release builds default to IL2CPP — if this project is still on Mono, note that as a finding, don't silently switch it.
2. Build an Android development build (APK). If no Android module is installed for this Unity version, install it via Unity Hub first and report that as a step, not a silent assumption.
3. Install and launch it on a connected device or emulator (Android Studio's emulator is fine if nothing physical is connected).
4. Confirm, on the actual build: `GameDatabase.LoadAll()` completes without error (watch specifically for `Newtonsoft.Json` reflection failures — this is the single most likely IL2CPP break point given how the data layer works), tap-to-move responds to a real touch, no crash on launch or on the first few interactions.
5. If IL2CPP breaks JSON loading: don't try to work around it blind. Report the exact error, and flag whether Newtonsoft's AOT-safe serialization settings or a `link.xml` preserve rule looks like the fix — but stop and report before making a build-wide change like disabling code stripping.
6. If everything works cleanly: log it and move on. This phase existing and passing is itself the deliverable — you're not adding features here.

---

## Phase 1 — Stubbed quest investigation (report only — do not build)

Nine of the 47 main-chain quests currently show a skip button instead of real content: fishing, crafting, exploration, waiting, witnessing an event, hosting a festival, a challenge run, trading with visitors, seed deposit, delivery, and generic (non-animal) item collection.

1. Read the actual `quests.json` entries for all 9 affected quests, starting with `q_first_catch`. For each: quote the actual objective/description text, the step type, and what NPC or location it's tied to if any.
2. For each quest, give a build-vs-simplify recommendation. Starting hypothesis to confirm or override against the real data:
   - **Build for real** (cheap, reuses existing systems): fishing (there's already a fish-tier tool in `tools.json` — this is a `ToolService` action, not a new system), waiting, delivery, generic item collection (extends the animal-produce-collect pattern), exploration (likely just chained `move_to` targets, which already exist).
   - **Simplify to existing verbs, don't build new systems**: crafting (no recipe data exists anywhere — inventing one from scratch for a single quest step is disproportionate), festival-hosting (needs new scheduling/multi-NPC logic for one step).
   - **Genuinely unclear until you read the data**: witnessing an event, challenge run, trading with visitors. Report what these actually require before recommending either path.
3. Write findings to `QUEST_GAP_REPORT.md` in the repo root. Include the quote, the step type, your recommendation, and your reasoning per quest.
4. **Stop here.** Do not implement anything from this phase. This decision affects the actual content of the game — it's not a technical judgment call to make unilaterally, even in an unattended run.

---

## Phase 2 — Build the tractable quest systems

Once Phase 1's report is reviewed and approved (or if running unattended and Phase 1's findings confirm the cheap-to-build hypothesis above), build:

1. Fishing via `ToolService` — tap a water-adjacent tile with the fish tool equipped, timer-based catch, same interaction pattern as farming.
2. A generic wait/timer step evaluator (game-time or real-time elapsed, same polling pattern as `restoration_above`).
3. Delivery — reuses `talk_to` plus an item-held check, not a new system.
4. Generic item collection — extend the existing animal-produce-collect pattern (`GameEvents.ItemGranted`) to non-animal world pickups (herbs/materials), with a simple spawnable pickup component.
5. Exploration — chain existing `move_to` targets against whatever locations the quest data specifies.

Real PlayMode tests per system, same evidence bar as the first 11 phases — actual state transitions in test output, not descriptions.

---

## Phase 3 — Resolve or cut the disproportionate systems

Based on Phase 1's findings:

1. For crafting and festival-hosting (and any of the "unclear" three that turn out to be similarly disproportionate): implement the simplification — reframe the quest's actual requirement using verbs you already have (e.g., festival-hosting as "talk to N specific NPCs within one in-game day" using the existing `NpcController` + `TalkedTo` event).
2. **Exception:** if Phase 1 flagged any of these as narratively central rather than a minor side-step — a capstone quest, a named character arc, something the GDD treats as important — stop and report rather than simplifying it unilaterally. Scope-cutting a throwaway fetch quest is a reasonable autonomous call; scope-cutting a story beat is not.

---

## Phase 4 — Animal region distribution

`animals.json` has no region field — all 15 animals currently spawn into one Starlight Vale paddock regardless of what region they thematically belong to.

1. Check whether the GDD or any other data file implies which animals belong to which region (palette/theming is a reasonable signal if nothing explicit exists).
2. Add a region assignment — either as a new field in `animals.json` if that's the cleaner fix, or a lookup table in code if you'd rather not touch the shipped data file. Your call, but log which you chose and why.
3. Update `AnimalSpawner` to place animals in their assigned region instead of the single vale paddock.
4. Verify: at least one animal now spawns in a non-Vale region, confirmed by reading scene/runtime state, not assumed from the spawner code.

---

## Phase 5 — Reuse the existing pixel-art pipeline

`D:\FarmandGiveGame\tools\art\` is a programmatic pixel-art generator built for the old web version of this game. Right now, Cosmic Bloom uses tinted placeholder shapes (`ProceduralSprites`) for everything — crops, NPCs, animals, tiles.

1. Read what's actually in `tools/art/` — what it generates, what inputs it takes, whether it's tied to the old web game's specific data shape or general enough to point at the same crop/NPC/animal/region JSON already powering Cosmic Bloom.
2. If it's genuinely reusable: wire it to generate real sprites for at least crops and NPCs (highest visual impact), replacing `ProceduralSprites` output for those categories.
3. If it's too tightly coupled to the old game's format to reuse cleanly: report that finding plainly rather than forcing a bad integration. Don't spend the whole phase fighting a tool that isn't the right fit — report and stop.
4. Verify with screenshots, same as every visual phase before this.

---

## Phase 6 — UI polish pass

1. Fix the dialogue box overlapping the bottom button bar (flagged from the last run).
2. Sweep all 5 UI screens for layout issues now that Phases 2-5 have added more content — more quest types, an animal-region map, more sprite variety. Check nothing overflows or overlaps that wasn't checked against a fuller game state before.

---

## Phase 7 — Full regression + extended definition of done

1. Re-run the entire test suite plus everything new from Phases 0-6.
2. Extend `FullPlaythroughTests` (or add a new playthrough test) that walks as far into the real 47-quest chain as is now genuinely completable — should be well past quest #7 once Phase 2/3 resolve the stubs, not stopping at the old Vale→Sun Garden boundary.
3. Update `BUILD_LOG.md` with a final status section matching the format from the first run: what's complete, what's still flagged, what needs human eyes.

---

## Still explicitly out of scope

Monetization/IAP wiring, final commissioned art (Phase 5 is reusing an existing tool, not commissioning new art), App Store/Play Store compliance, audio, cutscene polish beyond dialogue. None of that is worth building before the quest chain actually works end to end — wiring a paywall around content that doesn't fully exist yet is backwards.
