// Cosmic Bloom — places every region's landmarks (regions.json) at
// deterministic spots inside the region, outside the farmable core.
using System.Collections.Generic;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class LandmarkSpawner
    {
        // slots around the lush core, per landmark index within its region
        // (scaled 2x with the P3-Phase 15 region resize to 40x40). Public so
        // ReservedTiles can carve these fixed structures out of tillable ground.
        public static readonly Vector3[] Slots =
        {
            new(-13f, 11f), new(0f, 14f), new(13f, 11f), new(-13f, -11f), new(13f, -11f),
        };

        public static List<LandmarkController> SpawnAll(RegionManager regions, GameState state)
        {
            GameDatabase.LoadAll();
            var spawned = new List<LandmarkController>();

            foreach (var region in GameDatabase.Regions)
            {
                if (region.landmarks == null || !regions.TryGetBounds(region.id, out var bounds)) continue;
                for (int i = 0; i < region.landmarks.Count; i++)
                {
                    var pos = bounds.center + Slots[i % Slots.Length];
                    spawned.Add(LandmarkController.Spawn(region.landmarks[i], region.id, pos, state));
                }
            }

            Debug.Log($"[LandmarkSpawner] spawned {spawned.Count} landmarks");
            return spawned;
        }
    }
}
