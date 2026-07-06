// Cosmic Bloom — spawns the player's owned animals (save.animals) and
// handles purchases. animals.json has no shared/exclusive field, so the pool
// split is a code-side table (Phase 5, replacing Phase 4's one-home-region
// model) — see DECISION in BUILD_LOG. 9 animals are shared: once owned, one
// instance spawns in every region. 6 are region-exclusive: one instance,
// only in their assigned region — two of them in Nebula Garden, regions.json's
// last-in-progression region by both unlockCoins (6000) and unlockLevel (20).
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class AnimalSpawner
    {
        static readonly Dictionary<string, string> ExclusiveRegionByAnimal = new()
        {
            ["glow_axolotl"] = "starlight_vale",  // notes: lives in the restored Moonlit Pond
            ["asteroid_pig"] = "sun_garden",       // accent #8a6a3e exactly matches Sun Garden's ground tier
            ["crater_goat"] = "moon_garden",       // accent #7d84b8 exactly matches Moon Garden's ground tier
            ["star_owl"] = "star_garden",          // constellation/stargazing theming
            ["nebula_cow"] = "nebula_garden",      // name + magenta palette family (#e05ad8)
            ["star_slime"] = "nebula_garden",      // shares nebula_cow's exact accent hex #e05ad8
        };

        /// <summary>Shared (no entry) spawns everywhere; exclusive spawns only in its one region.</summary>
        public static bool CanSpawnInRegion(string animalId, string regionId) =>
            !ExclusiveRegionByAnimal.TryGetValue(animalId, out var home) || home == regionId;

        static readonly List<AnimalController> _spawned = new();
        static readonly Dictionary<string, int> _countByRegion = new();

        public static IReadOnlyList<AnimalController> Spawned => _spawned;

        /// <summary>Test/domain-reload hygiene (mirrors PickupSpawner/ExploreMarkerSpawner's
        /// ClearAll) — without this, a test that buys animals and only destroys its own
        /// GameObjects still leaves dangling entries in the static _spawned list, which
        /// throws off any later test asserting an exact Spawned.Count.</summary>
        public static void ClearAll()
        {
            foreach (var a in _spawned) if (a != null) Object.Destroy(a.gameObject);
            _spawned.Clear();
            _countByRegion.Clear();
        }

        public static void SpawnOwned(GameState state, RegionManager regions)
        {
            foreach (var old in _spawned.Where(a => a != null)) Object.Destroy(old.gameObject);
            _spawned.Clear();
            _countByRegion.Clear();
            foreach (var id in state.Save.animals)
                SpawnIntoEligibleRegions(id, state, regions);
            Debug.Log($"[AnimalSpawner] spawned {_spawned.Count} owned animal instances");
        }

        public static bool TryBuy(GameState state, RegionManager regions, string animalId)
        {
            var def = GameDatabase.Animals.FirstOrDefault(a => a.id == animalId);
            if (def == null || state.Save.animals.Contains(animalId)) return false;
            if (state.Save.level < def.unlockLevel) return false;
            if (!state.SpendCoins(def.cost)) return false;

            state.Save.animals.Add(animalId);
            state.MarkDirty();
            SpawnIntoEligibleRegions(animalId, state, regions);
            GameEvents.AnimalBought(animalId);
            Debug.Log($"[AnimalSpawner] bought {animalId} (-{def.cost}c)");
            return true;
        }

        static void SpawnIntoEligibleRegions(string animalId, GameState state, RegionManager regions)
        {
            foreach (var region in GameDatabase.Regions)
                TrySpawnOne(animalId, region.id, state, regions);
        }

        static bool TrySpawnOne(string animalId, string regionId, GameState state, RegionManager regions)
        {
            if (!CanSpawnInRegion(animalId, regionId)) return false;
            var def = GameDatabase.Animals.FirstOrDefault(a => a.id == animalId);
            if (def == null) return false;
            if (!regions.TryGetBounds(regionId, out var bounds)) return false;

            // stable little paddock spots below the lush core, deterministic by index within the region
            int i = _countByRegion.TryGetValue(regionId, out var c) ? c : 0;
            _countByRegion[regionId] = i + 1;
            var home = bounds.center + new Vector3(-5f + (i % 6) * 2f, -6.5f + (i / 6) * 2f, 0f);
            _spawned.Add(AnimalController.Spawn(def, home, state));
            return true;
        }
    }
}
