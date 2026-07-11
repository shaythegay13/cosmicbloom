// Cosmic Bloom — P3-Phase 16 DECISION: NPC daily schedules are a code-side
// table, not an npcs.json field (same pattern as ExploreTargets and
// AnimalSpawner.ExclusiveRegionByAnimal). Schedule stops are world positions,
// and npcs.json carries no coordinates anywhere — every position in the game
// is derived at runtime from RegionWorldBuilder's geometry consts. Writing
// coordinates into data would create a second source of truth that silently
// breaks on the next region resize (the P3-Phase 15 lesson). Deriving the
// three stops generically (home = spawn ring point, work = same bearing just
// outside the lush farm core, social = one shared plaza per region) also
// means no invented per-NPC content across 50 NPCs.
//
// Stops are period-driven via TimeManager.DayPeriod, respecting each NPC's
// existing activeAt window: day folk work the fields in the morning, gather
// at the plaza at midday, and head home for the evening; night folk mirror
// that into their waking window; "any" folk take a second work shift.
using UnityEngine;
using CosmicBloom.Core;

namespace CosmicBloom.World
{
    public static class NpcSchedule
    {
        public enum Stop { Home, Work, Social }

        // Offsets relative to region center. These must scale with any future
        // region resize alongside the NPC ring radius (see CLAUDE.md list).
        public const float HomeRadius = 14.4f;   // = NpcController spawn ring
        public const float WorkRadius = 9.5f;    // just outside the ±8 lush core, in the mid ring
        public static readonly Vector3 SocialOffset = new Vector3(0f, -11f, 0f);   // shared plaza south of the fields
        public const float SocialScatter = 2.5f; // per-NPC ring around the plaza — without it a
                                                 // region's whole roster stacks on one point and
                                                 // renders as a single NPC (found in live playtest)

        /// <summary>Where an NPC with this activeAt window should be during this period.</summary>
        public static Stop StopFor(string activeAt, TimeManager.DayPeriod period) => activeAt switch
        {
            "night" => period switch
            {
                TimeManager.DayPeriod.Evening => Stop.Work,
                TimeManager.DayPeriod.Night => Stop.Social,
                _ => Stop.Home,
            },
            "any" => period switch
            {
                TimeManager.DayPeriod.Morning => Stop.Work,
                TimeManager.DayPeriod.Midday => Stop.Social,
                TimeManager.DayPeriod.Evening => Stop.Work,
                _ => Stop.Home,
            },
            // "day" and anything unrecognized
            _ => period switch
            {
                TimeManager.DayPeriod.Morning => Stop.Work,
                TimeManager.DayPeriod.Midday => Stop.Social,
                _ => Stop.Home,
            },
        };

        /// <summary>
        /// World position of a stop for an NPC whose home sits at ringAngle on
        /// its region's spawn ring. Home/Work share the bearing so the walk to
        /// work reads as "leaving the house toward the fields"; Social is one
        /// shared spot per region so NPCs visibly gather.
        /// </summary>
        public static Vector3 StopPosition(Stop stop, Vector3 regionCenter, float ringAngle) => stop switch
        {
            Stop.Work => regionCenter + new Vector3(Mathf.Cos(ringAngle) * WorkRadius, Mathf.Sin(ringAngle) * WorkRadius, 0f),
            Stop.Social => regionCenter + SocialOffset
                + new Vector3(Mathf.Cos(ringAngle) * SocialScatter, Mathf.Sin(ringAngle) * SocialScatter, 0f),
            _ => regionCenter + new Vector3(Mathf.Cos(ringAngle) * HomeRadius, Mathf.Sin(ringAngle) * HomeRadius, 0f),
        };
    }
}
