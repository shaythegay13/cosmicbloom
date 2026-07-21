// Cosmic Bloom — P3-Phase 17: cells that free-form tilling must avoid so the
// player can't plant a crop on top of a fixed structure. Region-local cell
// coords, and identical for every region because both sources are region-
// relative offsets from the region center (Size/2, Size/2):
//   * landmark Slots (LandmarkSpawner) — one-off restored structures,
//   * the shared NPC social plaza (NpcSchedule.SocialOffset + scatter ring).
//
// Per-NPC Home/Work schedule stops are deliberately NOT reserved (P3-Phase 17
// DECISION, see BUILD_LOG): unlike the plaza they are mobile, per-roster ring
// positions that vary by NPC count and time of day, and reserving every one
// would arbitrarily fragment the newly free-form farmland. NPCs already walk
// across the farmable area; tap routing hits their collider before the ground,
// so you interact with an NPC rather than tilling under it anyway. Landmarks
// and the plaza are the only fixed, always-present world fixtures.
using System.Collections.Generic;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class ReservedTiles
    {
        static HashSet<Vector2Int> _cells;

        public static bool IsReserved(int x, int y) => Cells.Contains(new Vector2Int(x, y));

        static HashSet<Vector2Int> Cells
        {
            get
            {
                if (_cells != null) return _cells;
                _cells = new HashSet<Vector2Int>();
                var center = new Vector2(RegionWorldBuilder.Size / 2f, RegionWorldBuilder.Size / 2f);

                // landmark footprints: a 3x3 block around each slot's cell (the
                // sprites are large; reserving neighbours keeps crops off them)
                foreach (var slot in LandmarkSpawner.Slots)
                    Reserve(center + new Vector2(slot.x, slot.y), 1);

                // social plaza: cover the ring NPCs scatter onto when they gather
                var plaza = center + new Vector2(NpcSchedule.SocialOffset.x, NpcSchedule.SocialOffset.y);
                Reserve(plaza, Mathf.CeilToInt(NpcSchedule.SocialScatter));

                return _cells;
            }
        }

        static void Reserve(Vector2 worldLocal, int radius)
        {
            int cx = Mathf.FloorToInt(worldLocal.x);
            int cy = Mathf.FloorToInt(worldLocal.y);
            for (int dy = -radius; dy <= radius; dy++)
                for (int dx = -radius; dx <= radius; dx++)
                    _cells.Add(new Vector2Int(cx + dx, cy + dy));
        }
    }
}
