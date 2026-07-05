// Cosmic Bloom — Phase 2 DECISION: crystal_cave, crystal_cave_depths, and
// crash_site are explore-step targets (q_lost_seed, q_crystal_depths,
// q_meteor_mystery) that do not exist as landmarks in regions.json — there is
// nowhere in data to read a coordinate from. Per the user-approved
// QUEST_GAP_REPORT.md ruling, this is a small code-side target-position
// table (the same pattern as LandmarkSpawner's Slots), not a data change.
// All three targets live in starlight_vale, where every Act-2 quest giver
// (orbit, elder_nova, juniper, lumina, captain_pebble) is based.
using System.Collections.Generic;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class ExploreTargets
    {
        // target id -> (home region, offset from that region's bounds center)
        static readonly Dictionary<string, (string region, Vector3 offset)> Targets = new()
        {
            { "crystal_cave", ("starlight_vale", new Vector3(8f, -3f)) },
            { "crystal_cave_depths", ("starlight_vale", new Vector3(8f, -6f)) },
            { "crash_site", ("starlight_vale", new Vector3(-8f, -3f)) },
        };

        public static bool TryGetPosition(string target, out Vector3 position)
        {
            position = default;
            if (target == null || !Targets.TryGetValue(target, out var t)) return false;
            if (RegionManager.Instance == null || !RegionManager.Instance.TryGetBounds(t.region, out var bounds))
                return false;
            position = bounds.center + t.offset;
            return true;
        }
    }
}
