// Cosmic Bloom — Phase 2: reconciles one visual ExploreMarkerController per
// active quest whose current step is "explore" (q_lost_seed, q_crystal_depths,
// q_meteor_mystery). Mirrors PickupSpawner's reconcile shape.
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class ExploreMarkerSpawner
    {
        static readonly Dictionary<string, ExploreMarkerController> _spawned = new();

        public static void ClearAll()
        {
            foreach (var ctrl in _spawned.Values)
                if (ctrl != null) Object.Destroy(ctrl.gameObject);
            _spawned.Clear();
        }

        public static void Sync(QuestEngine quests)
        {
            var desired = new HashSet<string>();
            foreach (var questId in quests.ActiveQuests())
            {
                var step = quests.CurrentStep(questId);
                if (step != null && step.type == "explore") desired.Add(questId);
            }

            foreach (var questId in _spawned.Keys.Where(id => !desired.Contains(id)).ToList())
            {
                if (_spawned[questId] != null) Object.Destroy(_spawned[questId].gameObject);
                _spawned.Remove(questId);
            }

            foreach (var questId in desired)
            {
                if (_spawned.ContainsKey(questId)) continue;
                var step = quests.CurrentStep(questId);
                if (!ExploreTargets.TryGetPosition(step.target, out var pos)) continue;
                _spawned[questId] = ExploreMarkerController.Spawn(pos);
            }
        }
    }
}
