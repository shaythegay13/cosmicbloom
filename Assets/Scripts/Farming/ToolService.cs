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

        /// <summary>Run a farm action through the equipped tool's area. Primary cell first;
        /// secondary cells apply only where valid (farmable + action available).</summary>
        public static int Perform(FarmingSystem farming, GameState state, string action, int x, int y, string cropId = null)
        {
            var tool = ActiveTool(state, action);
            int tier = tool?.tier ?? 1;
            int done = 0;

            foreach (var cell in AreaFor(action, tier, x, y))
            {
                if (!World.RegionWorldBuilder.IsFarmable(cell.x, cell.y)) continue;
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
