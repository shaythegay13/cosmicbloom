// Cosmic Bloom — P3-Phase 18: companions walk the SAME daily schedule
// infrastructure Phase 16 built for the 50 human NPCs. This file deliberately
// adds no new geometry and no new stop semantics — it delegates to NpcSchedule
// for both the period->stop mapping and the stop positions, so a future region
// resize or plaza move updates NPCs and companions together instead of leaving
// a second, silently-diverging copy (the P3-Phase 15 lesson).
//
// Two companion-specific details are all that's genuinely new:
//  1. Animals have no `activeAt` field (npcs.json has one, animals.json does
//     not), so every companion is treated as "any" — awake across the whole
//     day, which is what a pet underfoot should be. That gives them the
//     4-stop day: Work / Social / Work / Home.
//  2. A distinct ring-angle phase from NpcController's `+0.4f`, so a companion
//     never spawns exactly on top of a human NPC, and companions scatter
//     against each other at the shared plaza. Phase 16's live playtest proved
//     a shared bearing renders a whole roster as one sprite.
using UnityEngine;
using CosmicBloom.Core;

namespace CosmicBloom.World
{
    public static class CompanionSchedule
    {
        /// <summary>Companions have no activeAt in data — they keep an NPC "any"
        /// schedule (see file header). Kept as a named constant so the data gap is
        /// greppable if animals.json ever grows the field.</summary>
        public const string ImpliedActiveAt = "any";

        /// <summary>Phase offset for the companion ring, distinct from NpcController's 0.4f.</summary>
        public const float RingPhase = 1.9f;

        // Companions stand on their own ring, slightly INSIDE the human one at every
        // stop. A phase offset alone was not enough: with 7 companions against 10 NPC
        // slots, bearings inevitably near-coincide (companion 4 landed 0.16 units from
        // NPC slot 8 — visually stacked, the exact Phase 16 playtest bug). Separate
        // radii make the separation structural instead of arithmetic luck, and read
        // naturally: pets keep a bit closer in than people. All three derive from
        // NpcSchedule's constants, so a future region resize carries through here too.
        public const float RadiusFactor = 0.9f;
        public const float HomeRadius = NpcSchedule.HomeRadius * RadiusFactor;
        public const float WorkRadius = NpcSchedule.WorkRadius * RadiusFactor;
        /// <summary>Inner plaza ring — comfortably clear of NpcSchedule.SocialScatter.</summary>
        public const float SocialScatter = NpcSchedule.SocialScatter * 0.56f;

        /// <summary>Deterministic bearing for companion #index of `count` owned companions.</summary>
        public static float RingAngle(int index, int count) =>
            (Mathf.PI * 2f * index) / Mathf.Max(1, count) + RingPhase;

        /// <summary>Where a companion should be during this period.</summary>
        public static NpcSchedule.Stop StopFor(TimeManager.DayPeriod period) =>
            NpcSchedule.StopFor(ImpliedActiveAt, period);

        /// <summary>World position of a companion's stop: NpcSchedule's stop LAYOUT
        /// (same bearings-from-center scheme, same shared plaza point) on the companion
        /// ring radii above, so pets never stand exactly where a person stands.</summary>
        public static Vector3 StopPosition(NpcSchedule.Stop stop, Vector3 regionCenter, float ringAngle)
        {
            var bearing = new Vector3(Mathf.Cos(ringAngle), Mathf.Sin(ringAngle), 0f);
            return stop switch
            {
                NpcSchedule.Stop.Work => regionCenter + bearing * WorkRadius,
                // same plaza as the NPCs (NpcSchedule owns that offset), inner ring
                NpcSchedule.Stop.Social => regionCenter + NpcSchedule.SocialOffset + bearing * SocialScatter,
                _ => regionCenter + bearing * HomeRadius,
            };
        }
    }
}
