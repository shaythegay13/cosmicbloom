// Cosmic Bloom — spawns the player's owned animals (save.animals) and
// handles purchases. Owned animals live near the lush core of the start
// region (animals.json has no home-region field — see DECISION in BUILD_LOG).
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class AnimalSpawner
    {
        const string HomeRegion = "starlight_vale";
        static readonly List<AnimalController> _spawned = new();

        public static IReadOnlyList<AnimalController> Spawned => _spawned;

        public static void SpawnOwned(GameState state, RegionManager regions)
        {
            foreach (var old in _spawned.Where(a => a != null)) Object.Destroy(old.gameObject);
            _spawned.Clear();
            foreach (var id in state.Save.animals)
                SpawnOne(id, state, regions);
            Debug.Log($"[AnimalSpawner] spawned {_spawned.Count} owned animals");
        }

        public static bool TryBuy(GameState state, RegionManager regions, string animalId)
        {
            var def = GameDatabase.Animals.FirstOrDefault(a => a.id == animalId);
            if (def == null || state.Save.animals.Contains(animalId)) return false;
            if (state.Save.level < def.unlockLevel) return false;
            if (!state.SpendCoins(def.cost)) return false;

            state.Save.animals.Add(animalId);
            state.MarkDirty();
            SpawnOne(animalId, state, regions);
            GameEvents.AnimalBought(animalId);
            Debug.Log($"[AnimalSpawner] bought {animalId} (-{def.cost}c)");
            return true;
        }

        static void SpawnOne(string animalId, GameState state, RegionManager regions)
        {
            var def = GameDatabase.Animals.FirstOrDefault(a => a.id == animalId);
            if (def == null) return;
            if (!regions.TryGetBounds(HomeRegion, out var bounds)) return;

            // stable little paddock spots below the lush core, deterministic by index
            int i = _spawned.Count;
            var home = bounds.center + new Vector3(-5f + (i % 6) * 2f, -6.5f + (i / 6) * 2f, 0f);
            _spawned.Add(AnimalController.Spawn(def, home, state));
        }
    }
}
