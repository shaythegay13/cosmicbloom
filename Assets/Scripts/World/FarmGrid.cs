// Cosmic Bloom — one FarmGrid per region. P3-Phase 17 (free-form placement):
// plots are no longer a fixed 256-cell lush-core grid. A FarmPlot view is
// created ONLY for a cell that actually has a plot — the ones already tilled in
// the save on load, and new ones on demand when the player tills bare ground.
// The ~1600 cells/region are never eagerly instantiated. State still lives in
// the save's per-region FarmState.plots via FarmingSystem — one shared data
// structure, no parallel state.
using System.Collections.Generic;
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
        readonly Dictionary<string, FarmPlot> _views = new();   // "x,y" -> view

        // Registry so PlayerController's free-form till path can find the current
        // region's grid without FarmGrid being wired into the player at spawn.
        static readonly Dictionary<string, FarmGrid> _active = new();
        public static FarmGrid ForRegion(string regionId) =>
            regionId != null && _active.TryGetValue(regionId, out var fg) ? fg : null;

        public static FarmGrid Create(string regionId, Grid grid, FarmingSystem farming, GameState state)
        {
            var go = new GameObject($"FarmGrid_{regionId}");
            go.transform.SetParent(grid.transform, false);
            var fg = go.AddComponent<FarmGrid>();
            fg.RegionId = regionId;
            fg._grid = grid;
            fg._farming = farming;
            fg._state = state;
            _active[regionId] = fg;
            // A till from ANY path (player tap, quest scripting, a test) materialises
            // the view — FarmGrid doesn't have to be the one that called Till.
            farming.OnAction += fg.OnFarmAction;
            fg.EnsureViewsForAllPlots();   // materialise plots restored from the save
            return fg;
        }

        void OnFarmAction(string action, int x, int y)
        {
            if (action == "till") EnsureViewsForAllPlots();
        }

        void OnDestroy()
        {
            if (_farming != null) _farming.OnAction -= OnFarmAction;
            if (RegionId != null && _active.TryGetValue(RegionId, out var cur) && cur == this)
                _active.Remove(RegionId);
        }

        /// <summary>Create a FarmPlot view for every plot in this region's save that
        /// doesn't have one yet. Cheap and idempotent — called on load and after any
        /// till (a tiered tool can till several cells at once).</summary>
        void EnsureViewsForAllPlots()
        {
            foreach (var key in _state.FarmFor(RegionId).plots.Keys)
            {
                if (_views.ContainsKey(key)) continue;
                var parts = key.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                    _views[key] = FarmPlot.Create(transform, _grid, RegionId, x, y, _farming, _state);
            }
        }

        /// <summary>Is this world point a fresh, tillable ground cell in this region?
        /// Fails for already-tilled cells (they own a FarmPlot collider that handles
        /// interaction directly) and for reserved/out-of-bounds cells.</summary>
        public bool TryTargetTill(Vector3 world, out Vector3 interactPos, out Vector2Int cell)
        {
            interactPos = default;
            cell = default;
            var c = _grid.WorldToCell(world);
            if (!RegionWorldBuilder.IsTillable(c.x, c.y)) return false;
            if (_state.FarmFor(RegionId).plots.TryGetValue($"{c.x},{c.y}", out var p) && p.tilled) return false;
            cell = new Vector2Int(c.x, c.y);
            interactPos = _grid.GetCellCenterWorld(c);
            return true;
        }

        /// <summary>Till the given cell (through the equipped tool's area) and spawn a
        /// view for every plot the till created. Returns true if anything was tilled.</summary>
        public bool TillAt(Vector2Int cell)
        {
            int done = ToolService.Perform(_farming, _state, "till", cell.x, cell.y);
            EnsureViewsForAllPlots();
            return done > 0;
        }

        /// <summary>Live crop facts (grow time / day-night gate / stage) for the plot at
        /// this world point, or a blank HasCrop=false when there's no crop there
        /// (P3-Phase 21). Sits alongside ContextActionAt on purpose: the grid already
        /// owns both the world→cell mapping and the GameState, so callers don't
        /// re-derive either.</summary>
        public Farming.CropInfo CropInfoAt(Vector3 world)
        {
            var c = _grid.WorldToCell(world);
            return Farming.CropInfo.For(_state, RegionId, c.x, c.y);
        }

        /// <summary>What tapping this world point would do right now — "till"/"plant"/
        /// "water"/"harvest", or null if nothing (bare non-tillable ground, or a plot
        /// mid-growth with no action available). Drives <see cref="Farming.FarmActionPrompt"/>
        /// (P3-Phase 17 live-verify finding D). Deliberately checks IsTillable for bare
        /// ground rather than trusting FarmingSystem.ActionFor alone — that method
        /// returns "till" for ANY untilled cell with no bounds/reserved check, which
        /// would otherwise mislabel a landmark/plaza tile as tillable.</summary>
        public string ContextActionAt(Vector3 world)
        {
            var c = _grid.WorldToCell(world);
            bool tilled = _state.FarmFor(RegionId).plots.TryGetValue($"{c.x},{c.y}", out var p) && p.tilled;
            if (tilled) return _farming.ActionFor(c.x, c.y);
            return RegionWorldBuilder.IsTillable(c.x, c.y) ? "till" : null;
        }
    }
}
