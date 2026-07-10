// Cosmic Bloom — region unlock + travel, driven by regions.json.
//
// DECISION (overnight run): unlocking costs unlockCoins and requires
// unlockLevel, exactly as regions.json specifies. Restoration score gates the
// QUEST CHAIN (restoration_above steps) which supplies the coins/xp — the
// build spec's assumption of a direct restoration threshold on region access
// doesn't match the shipped data, which has no per-region restoration
// requirement for unlocking. Flagged in BUILD_LOG for morning review.
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class RegionUnlockService
    {
        public static bool IsUnlocked(GameState state, string regionId) =>
            state.Save.unlockedRegions.Contains(regionId);

        public static bool MeetsLevel(GameState state, RegionDef def) =>
            state.Save.level >= def.unlockLevel;

        public static bool CanAfford(GameState state, RegionDef def) =>
            state.Save.coins >= def.unlockCoins;

        /// <summary>Pay the data-driven cost and open the region.</summary>
        public static bool TryUnlock(GameState state, string regionId)
        {
            var def = GameDatabase.Region(regionId);
            if (def == null || IsUnlocked(state, regionId)) return false;
            if (!MeetsLevel(state, def)) return false;
            if (!state.SpendCoins(def.unlockCoins)) return false;

            state.Save.unlockedRegions.Add(regionId);
            state.MarkDirty();
            GameEvents.RegionUnlocked(regionId);
            Debug.Log($"[Region] unlocked {regionId} (paid {def.unlockCoins}c at level {state.Save.level})");
            return true;
        }

        /// <summary>Travel to an unlocked region: current-region switch, player + camera move.</summary>
        public static bool TravelTo(GameState state, string regionId)
        {
            if (!IsUnlocked(state, regionId)) return false;
            var regions = RegionManager.Instance;
            if (regions == null || !regions.TryGetBounds(regionId, out var bounds)) return false;

            regions.SetCurrentRegion(regionId);
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.CancelMove();
                player.transform.position = bounds.center;
            }
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cam != null) cam.SnapTo(bounds.center);

            GameEvents.RegionEntered(regionId);
            Debug.Log($"[Region] traveled to {regionId}");
            return true;
        }
    }
}
