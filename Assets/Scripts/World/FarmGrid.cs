// Cosmic Bloom — one FarmGrid per region: instantiates a FarmPlot view for
// every lush-core cell (RegionWorldBuilder.IsFarmable), backed by the save's
// per-region FarmState.plots dictionary via FarmingSystem — a single shared
// data structure, no parallel state.
using CosmicBloom.Core;
using CosmicBloom.Farming;
using UnityEngine;

namespace CosmicBloom.World
{
    public class FarmGrid : MonoBehaviour
    {
        public string RegionId { get; private set; }

        Grid _grid;
        FarmingSystem _farming;
        GameState _state;

        public static FarmGrid Create(string regionId, Grid grid, FarmingSystem farming, GameState state)
        {
            var go = new GameObject($"FarmGrid_{regionId}");
            go.transform.SetParent(grid.transform, false);
            var fg = go.AddComponent<FarmGrid>();
            fg.RegionId = regionId;
            fg._grid = grid;
            fg._farming = farming;
            fg._state = state;
            fg.BuildPlots();
            return fg;
        }

        void BuildPlots()
        {
            for (int y = 0; y < RegionWorldBuilder.Size; y++)
                for (int x = 0; x < RegionWorldBuilder.Size; x++)
                {
                    if (!RegionWorldBuilder.IsFarmable(x, y)) continue;
                    FarmPlot.Create(transform, _grid, RegionId, x, y, _farming, _state);
                }
        }
    }
}
