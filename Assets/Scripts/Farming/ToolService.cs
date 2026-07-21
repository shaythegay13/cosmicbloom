// Cosmic Bloom — tool tiers from tools.json shape farming actions' area of
// effect. One resolver for all 10 tools; nothing per-tool beyond data.
//
// Area interpretation of tools.json notes (DECISION, overnight run):
//   till  t1/t2 → 1 cell        t3 ("3x1 row") → row of 3 centered on target
//   water t1 → 1 cell           t2 ("adjacent") → target + 4 neighbors
//                               t3 ("3x3 area") → 3x3 block
//   harvest t1 ("1x2 sweep") → target + cell above
//           t2 ("3x3 all ready") → 3x3 block
//   clear/fish tools don't map to farm-plot actions in this pass.
using System.Collections.Generic;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.Farming
{
    public static class ToolService
    {
        /// <summary>Best owned tool for an action unless the player picked one explicitly.</summary>
        public static ToolDef ActiveTool(GameState state, string action)
        {
            if (state.Save.selectedTools.TryGetValue(action, out var chosen))
            {
                var def = GameDatabase.Tools.FirstOrDefault(t => t.id == chosen);
                if (def != null && state.Save.tools.Contains(chosen)) return def;
            }
            return GameDatabase.Tools
                .Where(t => t.action == action && state.Save.tools.Contains(t.id))
                .OrderByDescending(t => t.tier)
                .FirstOrDefault();
        }

        public static void SelectTool(GameState state, string toolId)
        {
            var def = GameDatabase.Tools.FirstOrDefault(t => t.id == toolId);
            if (def == null || !state.Save.tools.Contains(toolId)) return;
            state.Save.selectedTools[def.action] = toolId;
            state.MarkDirty();
        }

        /// <summary>Cells an action touches, given the equipped tool's tier.</summary>
        public static IEnumerable<Vector2Int> AreaFor(string action, int tier, int x, int y)
        {
            switch (action)
            {
                case "till" when tier >= 3:
                    return new[] { new Vector2Int(x - 1, y), new Vector2Int(x, y), new Vector2Int(x + 1, y) };
                case "water" when tier >= 3:
                    return Block3x3(x, y);
                case "water" when tier == 2:
                    return new[]
                    {
                        new Vector2Int(x, y), new Vector2Int(x - 1, y), new Vector2Int(x + 1, y),
                        new Vector2Int(x, y - 1), new Vector2Int(x, y + 1),
                    };
                case "harvest" when tier >= 2:
                    return Block3x3(x, y);
                case "harvest":
                    return new[] { new Vector2Int(x, y), new Vector2Int(x, y + 1) };
                default:
                    return new[] { new Vector2Int(x, y) };
            }
        }

        static IEnumerable<Vector2Int> Block3x3(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    yield return new Vector2Int(x + dx, y + dy);
        }

        /// <summary>Auto-buy-on-need (q_toolsmith fix, user-approved 2026-07-05): same
        /// pattern as CropSelection's seed top-up — no tool shop UI in scope, so any
        /// tool for this action the player's level qualifies for gets bought the next
        /// time that action is actually performed. Kept out of ActiveTool/UI-query
        /// paths (the Phase-9 crop-label bug was exactly this mistake for seeds).</summary>
        static void EnsureTools(GameState state, string action)
        {
            foreach (var t in GameDatabase.Tools.Where(t => t.action == action && t.cost > 0))
            {
                if (state.Save.tools.Contains(t.id) || state.Save.level < t.unlockLevel) continue;
                if (state.SpendCoins(t.cost))
                {
                    state.Save.tools.Add(t.id);
                    state.MarkDirty();
                    Debug.Log($"[ToolService] auto-bought {t.id} (-{t.cost}c) at level {state.Save.level}");
                }
            }
        }

        /// <summary>Run a farm action through the equipped tool's area. Primary cell first;
        /// secondary cells apply only where valid (farmable + action available).</summary>
        public static int Perform(FarmingSystem farming, GameState state, string action, int x, int y, string cropId = null)
        {
            EnsureTools(state, action);
            var tool = ActiveTool(state, action);
            int tier = tool?.tier ?? 1;
            int done = 0;

            foreach (var cell in AreaFor(action, tier, x, y))
            {
                // P3-Phase 17: free-form placement — any in-bounds ground cell is
                // tillable, not just the lush core. Reserved cells (landmarks/plaza)
                // are excluded from tilling; water/harvest AoE can't touch them anyway
                // since only tilled cells hold a plot.
                if (!World.RegionWorldBuilder.InBounds(cell.x, cell.y)) continue;
                if (action == "till" && World.ReservedTiles.IsReserved(cell.x, cell.y)) continue;
                bool ok = action switch
                {
                    "till" => farming.Till(cell.x, cell.y),
                    "water" => farming.Water(cell.x, cell.y),
                    "harvest" => farming.Harvest(cell.x, cell.y),
                    "plant" => cropId != null && farming.Plant(cell.x, cell.y, cropId),
                    _ => false,
                };
                if (ok) done++;
            }
            return done;
        }
    }
}
