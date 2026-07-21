// Cosmic Bloom — builds all five region tilemaps at runtime from regions.json.
// Same tiered-ring layout for every region: 40x40 cells (P3-Phase 15 resize,
// 2x linear from the original 20x20), outer wild ring (tier 0), transitional
// ring (tier 1), inner 16x16 lush core (tier 2 — the farmable area). Colors
// come from each region's groundTiers — nothing is hardcoded per region.
// Regions sit 80 world units apart on X (one region-width gap) so they never
// overlap and there is room for travel framing.
using System.Collections.Generic;
using CosmicBloom.Data;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CosmicBloom.World
{
    public static class RegionWorldBuilder
    {
        public const int Size = 40;          // cells per side
        public const int Spacing = 80;       // world units between region origins
        public const int LushMin = 12;       // inner core: cells 12..27 → 16x16 = 256 farmable plots
        public const int LushMax = 27;
        public const int MidMin = 6;         // transitional ring starts this far from the region edge

        /// <summary>Ground tier for a cell: 2 = lush core, 1 = mid ring, 0 = outer wild.</summary>
        public static int TierAt(int x, int y)
        {
            int edge = Mathf.Min(Mathf.Min(x, Size - 1 - x), Mathf.Min(y, Size - 1 - y));
            if (edge >= LushMin) return 2;
            if (edge >= MidMin) return 1;
            return 0;
        }

        /// <summary>Tier-2 lush core. Still used for decoration placement and tests;
        /// it is no longer a tilling gate (P3-Phase 17 free-form placement).</summary>
        public static bool IsFarmable(int x, int y) => TierAt(x, y) == 2;

        public static bool InBounds(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;

        /// <summary>P3-Phase 17: any in-bounds ground cell can be tilled (wild/mid/lush),
        /// except cells reserved for fixed structures (see <see cref="ReservedTiles"/>).
        /// Soil tier is a quality modifier, not a placement gate — FarmingSystem.Till
        /// seeds lower-tier cells with poorer starting soil.</summary>
        public static bool IsTillable(int x, int y) => InBounds(x, y) && !ReservedTiles.IsReserved(x, y);

        /// <summary>World-space origin of a region by its index in regions.json order.</summary>
        public static Vector3 OriginOf(int regionIndex) => new(regionIndex * Spacing, 0f, 0f);

        /// <summary>Build every region's Grid+Tilemap and register bounds. Returns grids by region id.</summary>
        public static Dictionary<string, Grid> BuildAll(RegionManager regions)
        {
            GameDatabase.LoadAll();
            var mat = ProceduralSprites.LitMaterial;
            var result = new Dictionary<string, Grid>();

            for (int i = 0; i < GameDatabase.Regions.Count; i++)
            {
                var def = GameDatabase.Regions[i];
                var origin = OriginOf(i);
                var grid = Build(def, origin, mat);
                result[def.id] = grid;
                AmbientDecorationSpawner.SpawnForRegion(def, origin, i, grid.transform);

                var min = grid.transform.position;
                var bounds = new Bounds(min + new Vector3(Size / 2f, Size / 2f), new Vector3(Size, Size));
                regions.RegisterRegion(def.id, bounds);
            }
            return result;
        }

        static Grid Build(RegionDef def, Vector3 origin, Material mat)
        {
            var gridGo = new GameObject(ToPascal(def.id));
            gridGo.transform.position = origin;
            var grid = gridGo.AddComponent<Grid>();

            var groundGo = new GameObject("Ground");
            groundGo.transform.SetParent(gridGo.transform, false);
            var tilemap = groundGo.AddComponent<Tilemap>();
            var renderer = groundGo.AddComponent<TilemapRenderer>();
            if (mat != null) renderer.sharedMaterial = mat;
            renderer.sortingOrder = 0;

            var tiles = new Tile[3];
            for (int t = 0; t < 3; t++)
            {
                tiles[t] = ScriptableObject.CreateInstance<Tile>();
                // Real generated texture mask (Tools/art/build-cosmic.mjs output) if it
                // loaded — a grayscale crack/tuft pattern tinted by Tile.color below, so
                // regions.json's groundTiers hex stays the single source of truth for
                // per-region color. Falls back to the flat placeholder square.
                tiles[t].sprite = GeneratedSprites.Tile(def.id, t) ?? ProceduralSprites.Square(32, 0);
                tiles[t].color = ProceduralSprites.ParseHex(
                    def.groundTiers != null && t < def.groundTiers.Count ? def.groundTiers[t] : null);
                tiles[t].name = $"{def.id}_tier{t}";
            }

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    tilemap.SetTile(new Vector3Int(x, y, 0), tiles[TierAt(x, y)]);

            return grid;
        }

        /// <summary>"starlight_vale" → "StarlightVale" (inverse of GameBootstrap.GridNameToRegionId).</summary>
        public static string ToPascal(string id)
        {
            var sb = new System.Text.StringBuilder(id.Length);
            bool up = true;
            foreach (char c in id)
            {
                if (c == '_') { up = true; continue; }
                sb.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            return sb.ToString();
        }
    }
}
