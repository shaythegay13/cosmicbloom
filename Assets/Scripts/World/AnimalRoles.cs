// Cosmic Bloom — P3-Phase 18: the behavioral split of the 15 animals into
// LIVESTOCK (bought, then freely placed anywhere valid — the Phase 17 model
// extended from crops to creatures) and COMPANIONS (wired onto Phase 16's
// NPC daily-schedule + dialogue infrastructure).
//
// DECISION (P3-Phase 18): this is a code-side table, not an animals.json
// field — same pattern as ExploreTargets, NpcSchedule and
// AnimalSpawner.ExclusiveRegionByAnimal. animals.json DOES already carry a
// `kind` field (livestock | pet | helper), but the user-approved role split
// deliberately disagrees with it in four places (2026-07-25):
//   - comet_duck   : kind=livestock  -> Companion  (pairs with glow_axolotl at the pond)
//   - bee_swarm    : kind=helper     -> Livestock
//   - butterfly_swarm: kind=helper   -> Livestock
//   - firefly_swarm: kind=helper     -> Livestock
// Rather than rewrite `kind` (which RanchUI displays as flavor, and which
// still carries the meaningful pet-vs-helper distinction the two-way split
// flattens), `kind` stays DESCRIPTIVE and AnimalRole is BEHAVIORAL. One field
// says what an animal is; this table says how it behaves. Overwriting `kind`
// would have destroyed the helper/pet information for no gain.
//
// DECISION (P3-Phase 18): the split is BEHAVIORAL, NOT ECONOMIC. Companions
// are not stripped of income — any produce their animals.json entry defines
// keeps working exactly as Phase 4b/Phase 8 defined it (per-species clock,
// one stream per purchase). Two companions have produce today: star_slime
// (stardust, 120min) and comet_duck (comet_egg, 45min). The alternative —
// zero-economy companions — would have deleted two existing income streams
// and one item from a shipped economy to enforce a category boundary that
// nothing in the game actually needs. Companions therefore have a REAL but
// SMALL economic role: 2 of 7 produce; the other 5 are pure social/character
// value. No produce data changed in this phase.
using System.Collections.Generic;

namespace CosmicBloom.World
{
    public enum AnimalRole
    {
        /// <summary>Bought, then placed on open ground by the player; produces on a timer.</summary>
        Livestock,
        /// <summary>Walks a daily schedule and talks; may or may not also produce.</summary>
        Companion,
    }

    public static class AnimalRoles
    {
        // Companions are the enumerated set; everything else is livestock, so a
        // future animals.json entry defaults to the placeable/producing role
        // rather than silently gaining a schedule it has no dialogue for.
        static readonly HashSet<string> CompanionIds = new()
        {
            "comet_duck",
            "moon_rabbit",
            "biolume_fox",
            "cosmic_tortoise",
            "glow_axolotl",
            "star_owl",
            "star_slime",
        };

        public static AnimalRole RoleOf(string animalId) =>
            animalId != null && CompanionIds.Contains(animalId) ? AnimalRole.Companion : AnimalRole.Livestock;

        public static bool IsCompanion(string animalId) => RoleOf(animalId) == AnimalRole.Companion;
        public static bool IsLivestock(string animalId) => RoleOf(animalId) == AnimalRole.Livestock;

        /// <summary>Read-only view of the companion roster, for tests and UI.</summary>
        public static IReadOnlyCollection<string> Companions => CompanionIds;
    }
}
