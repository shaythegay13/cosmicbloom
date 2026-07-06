// Cosmic Bloom — spawns the player's owned animals (save.animals) and
// handles purchases. animals.json has no home-region field, so placement
// uses a code-side lookup table (same pattern as ExploreTargets' position
// table) keyed off explicit notes, name/behavior theming, and exact palette
// hex matches against a region's groundTiers — see DECISION in BUILD_LOG.
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class AnimalSpawner
    {
        const string DefaultRegion = "starlight_vale";

        static readonly Dictionary<string, string> HomeRegionByAnimal = new()
        {
            ["chicken"] = "starlight_vale",
            ["sheep"] = "starlight_vale",
            ["comet_duck"] = "starlight_vale",       // notes: happiest at the restored Moonlit Pond
            ["glow_axolotl"] = "starlight_vale",     // notes: lives in the restored Moonlit Pond
            ["biolume_fox"] = "starlight_vale",      // free Act 1 tutorial companion (cost 0, unlockLevel 1)
            ["cosmic_tortoise"] = "starlight_vale",  // body #6fae62 near-matches Vale's lush ground tier #5fae62
            ["bee_swarm"] = "starlight_vale",
            ["butterfly_swarm"] = "starlight_vale",
            ["firefly_swarm"] = "starlight_vale",
            ["asteroid_pig"] = "sun_garden",         // accent #8a6a3e exactly matches Sun Garden's ground tier
            ["crater_goat"] = "moon_garden",         // accent #7d84b8 exactly matches Moon Garden's ground tier
            ["moon_rabbit"] = "moon_garden",         // nocturnal notes fit Moon Garden's nocturnal-farming theme
            ["star_owl"] = "star_garden",            // constellation/stargazing theming
            ["nebula_cow"] = "nebula_garden",        // name + magenta palette family (#e05ad8)
            ["star_slime"] = "nebula_garden",        // shares nebula_cow's exact accent hex #e05ad8
        };

        static string HomeRegionFor(string animalId) =>
            HomeRegionByAnimal.TryGetValue(animalId, out var region) ? region : DefaultRegion;

        static readonly List<AnimalController> _spawned = new();
        static readonly Dictionary<string, int> _countByRegion = new();

        public static IReadOnlyList<AnimalController> Spawned => _spawned;

        public static void SpawnOwned(GameState state, RegionManager regions)
        {
            foreach (var old in _spawned.Where(a => a != null)) Object.Destroy(old.gameObject);
            _spawned.Clear();
            _countByRegion.Clear();
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

            var regionId = HomeRegionFor(animalId);
            if (!regions.TryGetBounds(regionId, out var bounds) &&
                !regions.TryGetBounds(DefaultRegion, out bounds)) return;

            // stable little paddock spots below the lush core, deterministic by index within the region
            int i = _countByRegion.TryGetValue(regionId, out var c) ? c : 0;
            _countByRegion[regionId] = i + 1;
            var home = bounds.center + new Vector3(-5f + (i % 6) * 2f, -6.5f + (i / 6) * 2f, 0f);
            _spawned.Add(AnimalController.Spawn(def, home, state));
        }
    }
}
