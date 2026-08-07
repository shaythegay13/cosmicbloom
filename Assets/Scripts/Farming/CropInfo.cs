// Cosmic Bloom — P3-Phase 21 legibility: a planted crop used to expose none of
// the three numbers that actually govern it. Grow time, the day/night growth
// gate, and the current stage all lived only in crops.json + PlotState, so a
// player watching an unchanging sprite had no way to tell "still growing" from
// "stalled because it's a day crop and it's night" from "needs water".
//
// Every value here is READ FROM DATA — crops.json via GameDatabase.Crop and the
// live PlotState. Nothing is invented, estimated, or hardcoded per crop: this
// file has no crop ids in it at all.
//
// Split out from the display surfaces on purpose. FarmActionPrompt renders it in
// world space today; keeping the formatting here means a future panel/tooltip
// reuses the same strings instead of re-deriving them (the re-derivation trap
// called out in CLAUDE.md's verification discipline).
//
// The one-line Summary and the stacked WorldLines are built from the SAME three
// parts, so the two surfaces can never drift apart. The first live screenshot
// showed a single long line running off both edges of a 6.4-unit camera — hence
// the stacked form for the cramped world-space label.
using CosmicBloom.Core;
using CosmicBloom.Data;

namespace CosmicBloom.Farming
{
    /// <summary>A read-only snapshot of what a planted plot is doing right now.</summary>
    public readonly struct CropInfo
    {
        public readonly bool HasCrop;
        public readonly string CropName;
        /// <summary>1-based stage for display; <see cref="Stages"/> is the mature stage.</summary>
        public readonly int Stage;
        public readonly int Stages;
        /// <summary>Total minutes from planting to mature, straight from crops.json.</summary>
        public readonly float GrowMinutes;
        /// <summary>"day" | "night" | "any", straight from crops.json.</summary>
        public readonly string GrowsAt;
        public readonly bool NeedsWater;
        public readonly bool Mature;
        /// <summary>True when the crop can't advance right now purely because its
        /// day/night window is shut — the state that previously looked identical to
        /// "growing normally" and drove the "is it broken?" confusion.</summary>
        public readonly bool WindowShut;

        CropInfo(CropDef def, CropState crop)
        {
            HasCrop = true;
            CropName = def.name;
            Stages = def.stages;
            Stage = crop.stage + 1;               // 0-based in state, 1-based on screen
            GrowMinutes = def.growMinutes;
            GrowsAt = def.growsAt;
            NeedsWater = crop.needsWater;
            Mature = crop.stage >= def.stages - 1;
            WindowShut = !Mature && !crop.needsWater &&
                         !(TimeManager.Instance?.GrowWindowOpen(def.growsAt) ?? true);
        }

        /// <summary>Info for the plot at (x,y) in `regionId`, or a HasCrop=false blank
        /// when the cell has no plot, no crop, or an id crops.json doesn't know.</summary>
        public static CropInfo For(GameState state, string regionId, int x, int y)
        {
            if (state == null || regionId == null) return default;
            if (!state.FarmFor(regionId).plots.TryGetValue($"{x},{y}", out var plot)) return default;
            if (plot?.crop == null) return default;
            var def = GameDatabase.Crop(plot.crop.id);
            return def == null ? default : new CropInfo(def, plot.crop);
        }

        // ── the three parts every surface is built from ───────────────────────

        /// <summary>"Star Tomato · stage 2/4", or "Star Tomato · ripe!" when mature.</summary>
        public string NameAndStage =>
            HasCrop ? $"{CropName} · {(Mature ? "ripe!" : $"stage {Stage}/{Stages}")}" : string.Empty;

        /// <summary>"12m total · ☀ day only" — grow time plus the growth window.</summary>
        public string TimeAndWindow =>
            HasCrop ? $"{GrowMinutes:0.#}m total · {WindowText}" : string.Empty;

        /// <summary>The short growth-window clause, straight from crops.json's growsAt.</summary>
        public string WindowText => GrowsAt switch
        {
            "day" => "☀ day only",
            "night" => "☾ night only",
            _ => "grows anytime",
        };

        /// <summary>What's actually blocking it right now, or "" if nothing is. This is
        /// the line that makes a stalled crop read as waiting rather than broken.</summary>
        public string StatusNote
        {
            get
            {
                if (!HasCrop || Mature) return string.Empty;
                if (NeedsWater) return "needs water";
                if (WindowShut) return GrowsAt == "night" ? "waiting for nightfall" : "waiting for sunrise";
                return string.Empty;
            }
        }

        /// <summary>One compact line — for panels/tooltips/logs with horizontal room.
        /// Empty string when there's no crop, so callers can concatenate blindly.</summary>
        public string Summary
        {
            get
            {
                if (!HasCrop) return string.Empty;
                string note = StatusNote;
                return $"{NameAndStage} · {TimeAndWindow}{(note.Length > 0 ? $" · {note}" : string.Empty)}";
            }
        }

        /// <summary>The same facts stacked into short lines, for the world-space label
        /// that has to fit inside a ~6-unit-wide camera without running off both edges.</summary>
        public string WorldLines
        {
            get
            {
                if (!HasCrop) return string.Empty;
                string note = StatusNote;
                return $"{NameAndStage}\n{TimeAndWindow}{(note.Length > 0 ? $"\n{note}" : string.Empty)}";
            }
        }
    }
}
