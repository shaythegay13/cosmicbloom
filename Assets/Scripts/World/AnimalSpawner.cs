// Cosmic Bloom — spawns the player's owned animals (save.animals) and
// handles purchases. animals.json has no shared/exclusive field, so the pool
// split is a code-side table (Phase 5, replacing Phase 4's one-home-region
// model) — see DECISION in BUILD_LOG. 9 animals are shared: once owned, one
// instance spawns in every region. 6 are region-exclusive: one instance,
// only in their assigned region — two of them in Nebula Garden, regions.json's
// last-in-progression region by both unlockCoins (6000) and unlockLevel (20).
//
// P3-Phase 18 adds the role split (see AnimalRoles): LIVESTOCK can be placed
// on any open ground cell by the player and otherwise falls back to the
// deterministic paddock spot; COMPANIONS ignore placement entirely and take a
// bearing on the companion schedule ring instead (AnimalController walks them
// between NpcSchedule stops from there).
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

        /// <summary>Save key for one animal's placement in one region.</summary>
        public static string PlacementKey(string regionId, string animalId) => $"{regionId}:{animalId}";

        /// <summary>Test/domain-reload hygiene (mirrors PickupSpawner/ExploreMarkerSpawner's
        /// ClearAll) — without this, a test that buys animals and only destroys its own
        /// GameObjects still leaves dangling entries in the static _spawned list, which
        /// throws off any later test asserting an exact Spawned.Count.</summary>
        public static void ClearAll()
        {
            foreach (var a in _spawned) if (a != null) Object.Destroy(a.gameObject);
            _spawned.Clear();
            _countByRegion.Clear();
            AnimalPlacement.Disarm();
            AnimalController.ResetProduceClocks();
        }

        public static void SpawnOwned(GameState state, RegionManager regions)
        {
            foreach (var old in _spawned.Where(a => a != null)) Object.Destroy(old.gameObject);
            _spawned.Clear();
            _countByRegion.Clear();
            AnimalController.ResetProduceClocks();
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

        // ---------------------------------------------------------------- placement

        /// <summary>
        /// P3-Phase 18 DECISION — livestock place on OPEN GROUND, not on tilled soil.
        /// A cell is valid when it is in-bounds and unreserved (RegionWorldBuilder.
        /// IsTillable — the exact same gate Phase 17 uses for crops, so livestock and
        /// crops share one definition of "real ground"), AND holds no tilled plot,
        /// AND holds no other animal.
        ///
        /// Requiring tilled soil instead was rejected for three reasons: a tilled cell
        /// already owns a FarmPlot collider that claims the tap for plant/water/harvest,
        /// so an animal standing there would be unreachable; making pasture compete with
        /// crops for the same cells manufactures a resource conflict the design never
        /// asked for; and livestock on open pasture is the genre-natural read. Soil tier
        /// is ignored entirely — unlike crops, an animal has no soil-quality dimension.
        /// </summary>
        public static bool CanPlaceAt(GameState state, string regionId, int x, int y, string movingAnimalId = null)
        {
            if (state == null || regionId == null) return false;
            if (!RegionWorldBuilder.IsTillable(x, y)) return false;
            if (state.FarmFor(regionId).plots.TryGetValue($"{x},{y}", out var plot) && plot.tilled) return false;

            string self = movingAnimalId != null ? PlacementKey(regionId, movingAnimalId) : null;
            string cell = $"{x},{y}";
            string prefix = regionId + ":";
            foreach (var kv in state.Save.animalPlacements)
            {
                if (kv.Key == self) continue;
                if (kv.Key.StartsWith(prefix) && kv.Value == cell) return false;
            }
            return true;
        }

        /// <summary>Place an owned livestock animal on a region-local cell, persisting it
        /// and moving the live instance. Returns false if the animal isn't owned, isn't
        /// livestock, can't live in this region, or the cell is invalid.</summary>
        public static bool TryPlace(GameState state, RegionManager regions, string regionId,
                                    string animalId, int x, int y)
        {
            if (state == null || !state.Save.animals.Contains(animalId)) return false;
            if (!AnimalRoles.IsLivestock(animalId)) return false;
            if (!CanSpawnInRegion(animalId, regionId)) return false;
            if (!CanPlaceAt(state, regionId, x, y, animalId)) return false;

            state.Save.animalPlacements[PlacementKey(regionId, animalId)] = $"{x},{y}";
            state.MarkDirty();

            if (regions != null && regions.TryGetBounds(regionId, out var bounds))
            {
                var world = RegionWorldBuilder.CellCenterWorld(bounds.center, x, y);
                var live = _spawned.FirstOrDefault(
                    a => a != null && a.Def.id == animalId && a.RegionId == regionId);
                if (live != null) live.MoveHome(world);
            }
            Debug.Log($"[AnimalSpawner] placed {animalId} at {regionId} ({x},{y})");
            return true;
        }

        /// <summary>Persisted placement cell for an animal in a region, if any.</summary>
        public static bool TryGetPlacement(GameState state, string regionId, string animalId, out Vector2Int cell)
        {
            cell = default;
            if (state == null) return false;
            if (!state.Save.animalPlacements.TryGetValue(PlacementKey(regionId, animalId), out var raw)) return false;
            var parts = raw.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
                return false;
            cell = new Vector2Int(x, y);
            return true;
        }

        // ---------------------------------------------------------------- spawning

        static void SpawnIntoEligibleRegions(string animalId, GameState state, RegionManager regions)
        {
            foreach (var region in GameDatabase.Regions)
                TrySpawnOne(animalId, region.id, state, regions);
        }

        /// <summary>Stable bearing slot for a companion: its index within the companion
        /// roster in animals.json order, over the full roster size. Fixed divisor so a
        /// companion's spot doesn't shuffle when a different companion is bought.</summary>
        static float CompanionRingAngle(string animalId)
        {
            var companions = GameDatabase.Animals.Where(a => AnimalRoles.IsCompanion(a.id)).ToList();
            int idx = companions.FindIndex(a => a.id == animalId);
            return CompanionSchedule.RingAngle(Mathf.Max(0, idx), companions.Count);
        }

        static bool TrySpawnOne(string animalId, string regionId, GameState state, RegionManager regions)
        {
            if (!CanSpawnInRegion(animalId, regionId)) return false;
            var def = GameDatabase.Animals.FirstOrDefault(a => a.id == animalId);
            if (def == null) return false;
            if (!regions.TryGetBounds(regionId, out var bounds)) return false;

            // paddock index is consumed by every animal regardless of role/placement, so
            // an unplaced animal's default spot never shifts because a neighbour moved
            int i = _countByRegion.TryGetValue(regionId, out var c) ? c : 0;
            _countByRegion[regionId] = i + 1;

            Vector3 home;
            float ringAngle = 0f;
            bool companion = AnimalRoles.IsCompanion(animalId);
            if (companion)
            {
                // companions live on the schedule ring; AnimalController snaps them to
                // whichever stop the current period calls for
                ringAngle = CompanionRingAngle(animalId);
                home = CompanionSchedule.StopPosition(NpcSchedule.Stop.Home, bounds.center, ringAngle);
            }
            else if (TryGetPlacement(state, regionId, animalId, out var cell))
            {
                home = RegionWorldBuilder.CellCenterWorld(bounds.center, cell.x, cell.y);
            }
            else
            {
                // stable little paddock spots below the lush core, deterministic by index
                // within the region (offsets scaled 2x with the P3-Phase 15 region resize)
                home = bounds.center + new Vector3(-10f + (i % 6) * 4f, -13f + (i / 6) * 4f, 0f);
            }

            _spawned.Add(AnimalController.Spawn(def, home, state, regionId, bounds.center, ringAngle));
            return true;
        }
    }
}
