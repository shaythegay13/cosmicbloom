// Cosmic Bloom — runtime world assembly. The scene stays minimal (managers,
// camera, light, region tilemaps); everything dynamic — player, and in later
// phases NPCs, animals, farm grids, UI — spawns here at Start from game data.
// This avoids per-phase scene surgery (scene YAML edits lost work twice on
// 2026-07-03) and keeps the whole world PlayMode-testable.
using CosmicBloom.World;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CosmicBloom.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        public PlayerController Player { get; private set; }

        void Start()
        {
            var regions = GetComponent<RegionManager>() != null
                ? GetComponent<RegionManager>()
                : gameObject.AddComponent<RegionManager>();

            RegisterSceneTilemapRegions(regions);
            SpawnPlayer(regions);
            AttachCamera();
        }

        /// <summary>Register every "<regionId>" Grid object that has a Tilemap under it.
        /// Phase 1: only StarlightVale exists; Phase 2 adds the other four.</summary>
        static void RegisterSceneTilemapRegions(RegionManager regions)
        {
            foreach (var grid in FindObjectsByType<Grid>(FindObjectsSortMode.None))
            {
                var tilemap = grid.GetComponentInChildren<Tilemap>();
                if (tilemap == null) continue;

                string regionId = GridNameToRegionId(grid.name);
                tilemap.CompressBounds();
                var cell = tilemap.cellBounds;
                Vector3 min = tilemap.CellToWorld(cell.min);
                Vector3 max = tilemap.CellToWorld(cell.max);
                var bounds = new Bounds((min + max) * 0.5f, max - min);
                regions.RegisterRegion(regionId, bounds);
                Debug.Log($"[Bootstrap] region '{regionId}' bounds min={bounds.min} max={bounds.max}");
            }
        }

        /// <summary>"StarlightVale" → "starlight_vale", "SunGarden" → "sun_garden", …</summary>
        static string GridNameToRegionId(string gridName)
        {
            var sb = new System.Text.StringBuilder(gridName.Length + 4);
            for (int i = 0; i < gridName.Length; i++)
            {
                char c = gridName[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        void SpawnPlayer(RegionManager regions)
        {
            Vector3 spawn = regions.TryGetBounds(regions.CurrentRegionId, out var b)
                ? b.center
                : Vector3.zero;
            Player = PlayerController.Spawn(spawn);
        }

        void AttachCamera()
        {
            var cam = Camera.main;
            if (cam == null || Player == null) return;
            var follow = cam.GetComponent<CameraFollow>() != null
                ? cam.GetComponent<CameraFollow>()
                : cam.gameObject.AddComponent<CameraFollow>();
            follow.target = Player.transform;
            follow.SnapTo(Player.transform.position);
        }
    }
}
