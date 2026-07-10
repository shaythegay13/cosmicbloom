// Cosmic Bloom — world-layout migrations for existing saves (P3-Phase 15).
// The region resize (20x20 → 40x40) moved the farmable lush core from cells
// 6..13 to 12..27, so plot keys written by the old world would no longer have
// a FarmPlot view. worldVersion is an ADDITIVE snapshot field (server schema
// stays 2 — validateSnapshot doesn't inspect it); saves without it (0) get
// their plot keys shifted +6/+6 once — mapping the old core exactly into the
// new one — and their stored player position dropped (old world coordinates
// land in the inter-region gap after the 40→80 spacing change; spawning at
// the region center is the fresh-save behavior). Runs only on SaveManager's
// load paths, never on serialize, and stamps worldVersion so it can't apply
// twice to the same snapshot.
using System.Collections.Generic;
using CosmicBloom.Core;

namespace CosmicBloom.Save
{
    public static class SaveMigration
    {
        public const int CurrentWorldVersion = 1;   // 1 = 40x40 regions (P3-Phase 15)
        public const int PlotCellOffset = 6;        // old lush core 6..13 → new 12..27

        /// <summary>Bring a loaded snapshot up to the current world layout.
        /// Returns true if anything changed (caller should persist).</summary>
        public static bool Apply(SaveSnapshot save)
        {
            if (save == null || save.worldVersion >= CurrentWorldVersion) return false;

            foreach (var farm in save.farms.Values)
            {
                if (farm?.plots == null || farm.plots.Count == 0) continue;
                var moved = new Dictionary<string, PlotState>();
                foreach (var kv in farm.plots)
                    moved[ShiftKey(kv.Key)] = kv.Value;
                farm.plots.Clear();
                foreach (var kv in moved) farm.plots[kv.Key] = kv.Value;
            }

            save.hasPlayerPos = false;
            save.worldVersion = CurrentWorldVersion;
            return true;
        }

        /// <summary>"x,y" → "x+6,y+6"; malformed keys pass through untouched.</summary>
        static string ShiftKey(string key)
        {
            var parts = key.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y))
                return key;
            return $"{x + PlotCellOffset},{y + PlotCellOffset}";
        }
    }
}
