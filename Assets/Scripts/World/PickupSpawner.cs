// Cosmic Bloom — Phase 2: reconciles world pickups against every active
// quest's current "collect" step (q_journals, q_lost_seed, q_crystal_depths,
// q_star_charts — see QUEST_GAP_REPORT.md). Deterministic per (questId,
// stepIndex, itemIndex) so a resumed save re-spawns exactly the remaining
// items at the same spots, not a fresh full set.
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class PickupSpawner
    {
        readonly struct Key
        {
            public readonly string questId;
            public readonly int stepIndex;
            public readonly int itemIndex;
            public Key(string q, int s, int i) { questId = q; stepIndex = s; itemIndex = i; }
        }

        static readonly Dictionary<Key, PickupController> _spawned = new();

        /// <summary>Test/domain-reload hygiene.</summary>
        public static void ClearAll()
        {
            foreach (var ctrl in _spawned.Values)
                if (ctrl != null) Object.Destroy(ctrl.gameObject);
            _spawned.Clear();
        }

        public static void Sync(GameState state, RegionManager regions, QuestEngine quests)
        {
            var desired = new HashSet<Key>();
            foreach (var questId in quests.ActiveQuests())
            {
                var step = quests.CurrentStep(questId);
                if (step == null || step.type != "collect" || QuestEngine.IsAnimalProduce(step.item)) continue;

                int stepIdx = quests.StepIndex(questId);
                int progress = quests.StepProgress(questId);
                int count = Mathf.Max(1, step.count);
                for (int i = progress; i < count; i++)
                    desired.Add(new Key(questId, stepIdx, i));
            }

            foreach (var key in _spawned.Keys.Where(k => !desired.Contains(k)).ToList())
            {
                if (_spawned[key] != null) Object.Destroy(_spawned[key].gameObject);
                _spawned.Remove(key);
            }

            foreach (var key in desired)
            {
                if (_spawned.ContainsKey(key)) continue;
                var def = GameDatabase.Quest(key.questId);
                if (def == null || key.stepIndex >= def.steps.Count) continue;
                var step = def.steps[key.stepIndex];

                string giverRegion = GameDatabase.Npc(def.giver)?.region ?? state.Save.currentRegion;
                if (!regions.TryGetBounds(giverRegion, out var bounds)) continue;

                var pos = SlotFor(bounds, key.questId, key.stepIndex, key.itemIndex);
                _spawned[key] = PickupController.Spawn(step.item, pos);
            }
        }

        /// <summary>Deterministic scatter within the region's wild ring, seeded by
        /// (questId, stepIndex, itemIndex) so resumed saves see the same spots.</summary>
        static Vector3 SlotFor(Bounds bounds, string questId, int stepIndex, int itemIndex)
        {
            var rng = new System.Random($"{questId}:{stepIndex}:{itemIndex}".GetHashCode());
            float x = Mathf.Lerp(bounds.min.x + 2f, bounds.max.x - 2f, (float)rng.NextDouble());
            float y = Mathf.Lerp(bounds.min.y + 2f, bounds.max.y - 2f, (float)rng.NextDouble());
            return new Vector3(x, y, 0f);
        }
    }
}
