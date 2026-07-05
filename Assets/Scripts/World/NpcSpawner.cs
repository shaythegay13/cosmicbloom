// Cosmic Bloom — spawns all 50 NPCs from npcs.json into their home regions.
// Called by GameBootstrap after regions are built.
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class NpcSpawner
    {
        public static List<NpcController> SpawnAll(RegionManager regions, Core.GameState state = null)
        {
            GameDatabase.LoadAll();
            var spawned = new List<NpcController>();

            foreach (var group in GameDatabase.Npcs.GroupBy(n => n.region))
            {
                if (!regions.TryGetBounds(group.Key, out var bounds))
                {
                    Debug.LogWarning($"[NpcSpawner] no region bounds for '{group.Key}' — {group.Count()} NPCs skipped");
                    continue;
                }
                var roster = group.ToList();
                for (int i = 0; i < roster.Count; i++)
                    spawned.Add(NpcController.Spawn(roster[i], bounds.center, i, roster.Count, state));
            }

            Debug.Log($"[NpcSpawner] spawned {spawned.Count} NPCs across " +
                      $"{spawned.Select(n => n.Def.region).Distinct().Count()} regions");
            return spawned;
        }
    }
}
