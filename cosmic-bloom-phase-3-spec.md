# Cosmic Bloom — Phase 3 Build Spec (Remaining Scope)

Continues from the Phase 2 spec, which is complete through Phase 7. **Phase numbering continues at 8, not 0** — restarting at 0 a third time is exactly how the "Phase 5 vs Phase 5" collision happened last spec. Don't repeat that.

**Same standing rules as before, not re-explained here:** evidence over claims, headless batchmode + PlayMode tests over live MCP, reuse existing patterns (test filters, build scripts, lookups) instead of rebuilding them from scratch — check `CLAUDE.md` and the most recent `BUILD_LOG.md` entries first. `BUILD_LOG.md` gets new dated entries appended.

**Cut from consideration entirely, not deferred:** multiplayer, localization, accessibility features. None of these make sense for a solo project with no players yet. Not in scope for this spec or any near-term one.

**Read this before Phase 11 or 12:** those two phases build code and draft documents. They do not make the game monetization-ready or store-ready on their own. Actual App Store/Play Store account enrollment, product registration, tax/banking setup, and submission are account-holder actions only Shay can do — no amount of autonomous work closes that gap. Treat those phases as "the code and documents are ready for the business-side work," not "this is done."

---

## Phase 8 — Close the open loose ends

Three specs in, several things are still flagged and never resolved. Close them before adding new scope.

1. **Investigate the two still-unresolved crash signatures** from earlier sessions: the `WorkerManagerASIO` shutdown/TCP hang, and the `DelayedCallManager`/coroutine crash. The render-surface crash is fixed and confirmed; these two are not — they were only ever logged as open. Root-cause them now, or at minimum determine reliable reproduction conditions and document why they haven't recurred if they haven't. Don't let a fourth spec inherit these as "still open" again without real investigation.
2. **Confirm or fix `q_festival_circuit`'s day-rollover behavior.** Partial progress resets at day rollover — check whether this was ever confirmed as a deliberate design call or just fell out of the implementation unreviewed. Decide now, one way or the other, and log it.
3. **Address the Phase 4b flagged economy issue:** shared animals can produce coins from up to 5 simultaneous instances across regions (up to 5x income). This was flagged, not fixed. Decide a real fix — a global cap on simultaneous shared-animal instances, diminishing returns per additional instance, or another approach — and implement it. Don't let this ride into monetization work unresolved.
4. **Confirm the human-only verification items from the original build were actually done:** the device-simulator visual pass, and camera/day-night feel at real timescale. These were explicitly flagged as needing a human, not code. If they were never actually checked, that's on the human side to close, but at minimum confirm their current status rather than let them sit silently unconfirmed indefinitely.

Model: Fable 5 for the crash investigation (this has resisted two lighter passes already — worth the reasoning depth this time). Sonnet 5 for the rest.

---

## Phase 9 — Extend the art pipeline to animals and tiles

Phase 5 proved `tools/art/` is reusable for crops and NPCs. Extend it to the two categories explicitly deferred at the time: animals (15) and ground tiles (currently flat procedural color rings per tier).

1. Generate real sprites for all 15 animals using the same deterministic pipeline and import path Phase 5 established.
2. Generate real ground tile art per region, replacing the flat procedural tier colors — check whether the old web game's tile assets (if any exist in `tools/art/`) can inform this, or whether this needs new generation logic within the same tool.
3. Verify with screenshots, same bar as every visual phase before this.
4. Full regression, reusing the known-good split filter lists from the most recent `phase*_split_*.xml` files.
5. Update `BUILD_LOG.md` and `CLAUDE.md`, commit.

Model: Sonnet 5. This is now a proven, bounded pattern — no reason to pay Fable rates for applying it a second time.

---

## Phase 10 — Basic audio pass

Neither prior spec touched audio. For a shippable game, silence is a real gap — but treat this narrowly.

1. Basic SFX only: till, plant, water, harvest, quest-complete, UI taps. Source these from royalty-free/CC0 sound libraries (e.g., freesound.org's CC0-filtered results, Kenney's asset packs) — don't attempt to generate SFX from scratch.
2. Simple ambient loop per region (5 total) — again, sourced from royalty-free/CC0 music libraries, not composed or AI-generated from nothing. Real music composition is a different skill and a different risk (quality and licensing both), and not something to delegate to an autonomous coding session.
3. Wire an audio manager: SFX triggers on the relevant existing events (already-established `GameEvents` pattern), region-based ambient track switching tied to the existing region-load logic.
4. Verify with a test confirming audio components are actually attached and triggered on the relevant events — can't screenshot sound, so assert on `AudioSource.isPlaying` or clip-assignment state at the right moments instead.
5. Full regression, commit.

Model: Sonnet 5. Mechanical wiring once sourcing is settled — flag if sourcing itself turns out to need real judgment calls (licensing questions, mood/fit for a specific region), and escalate only that part.

---

## Phase 11 — Monetization wiring (code only)

This is the code-side integration only. It does not include registering products in App Store Connect or Google Play Console — that's account-holder work, not something this phase produces.

1. Integrate Unity IAP (or the current-equivalent package — check what's actually current and supported for this Unity version before assuming a package name).
2. Define the products from the GDD: the $4.99 one-time full-game unlock (gating Act 2+ content behind it), and the cosmetic IAP category (structure the hooks; actual cosmetic items can stay minimal/placeholder for now — this phase is about the purchase-and-gate mechanism working, not a full cosmetic catalog).
3. Purchase restore logic — a real requirement for both app stores, not optional.
4. Gate Act 2+ content behind the unlock flag in `SaveManager`'s existing state, consistent with the schema-2 additive-fields rule already established.
5. Test using Unity IAP's sandbox/fake store mode — this can be verified in editor/headless without needing real store accounts. Don't attempt to test against real App Store/Play Store sandboxes; that requires developer accounts that don't exist yet.
6. Full regression, commit.

Model: Fable 5 for the initial integration design (payment flow correctness matters, worth the reasoning depth), Sonnet 5 for the mechanical gating-logic wiring once the approach is set.

---

## Phase 12 — Store compliance prep (documents only)

Draft, don't submit. Actual submission requires developer accounts that don't exist yet.

1. Draft a privacy policy reflecting what the game actually does: `ApiClient` talks to a backend for auth/save-sync, meaning real user data handling exists, meaning a real privacy policy is genuinely necessary, not boilerplate. Base it on the actual data flows in `ApiClient`/`SaveManager`, not a generic template. **Flag clearly in the output: this is a drafting aid, not a substitute for actual legal review before publishing.** Neither Claude Code nor I are lawyers, and this is a document with real legal weight once the app ships.
2. Prepare a content-rating questionnaire answer sheet (age rating inputs for both stores) based on the game's actual content — no violence, gambling mechanics, or mature themes, so this should be straightforward, but document the actual answers rather than assume.
3. Compile a submission checklist: required screenshots, app icon specs, store listing copy draft, required metadata — as a checklist Shay can work through once developer accounts exist, not as completed submissions.

Model: Fable 5 for the privacy policy draft given the real legal stakes in getting the data-flow description accurate. Sonnet 5 for the checklist compilation.

---

## Phase 13 — QA/polish pass and playtest prep

Last phase before this is genuinely ready for real human playtesting, not further autonomous building.

1. Full regression one more time, plus a broader manual bug sweep across everything built since the original overnight build — with particular attention to interactions between systems built in different phases (e.g., does the Phase 11 unlock-gate interact correctly with Phase 8's festival day-rollover fix, does Phase 9's new animal art affect Phase 6's UI layout fixes).
2. Balance pass reflecting Phase 8's economy fix — confirm the fix actually changes observed income rates in a test, not just that the cap exists in code.
3. Produce a structured playtest checklist for Shay to actually run through personally — a real end-to-end session, not another automated test. This is explicitly a human task, not something to delegate further.
4. Final `BUILD_LOG.md` status section: full project state, everything resolved, everything still open, everything requiring human action before this could ship.

Model: Sonnet 5, escalate specific bugs to Fable 5 only if something resists a first fix attempt.

---

## Phase 14: main menu, settings, and onboarding. Never scoped in any prior spec across this project — a real gap, not something deferred.

1. Title/main menu: New Game, Continue (if a save exists), Settings, Quit. Right now the game boots straight into SampleScene with nothing before it.
2. Settings screen: audio volume control at minimum (relevant once Phase 10 lands), plus anything simple to expose now.
3. Basic onboarding: a short, skippable first-time intro. Check whether the existing q_arrival quest can carry this rather than building a separate tutorial system from scratch.
4. Screenshot verification for menu/settings screens.
5. Full regression, reusing known-good split filters.
6. Update BUILD_LOG.md and CLAUDE.md, commit.

---

## What "done" looks like after this spec

Every system built and tested, real art on crops/NPCs/animals/tiles, basic audio, monetization code wired and gate-tested in sandbox mode, compliance documents drafted. **Still required after this, and not achievable by any spec:** developer account enrollment on both stores, actual legal review of the privacy policy, real payment sandbox testing against live store infrastructure, and an actual human sitting down and playing the finished thing.
