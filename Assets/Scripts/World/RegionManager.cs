// Cosmic Bloom — tracks each region's world-space bounds and the player's
// current region. Region tilemaps register themselves here; movement is
// clamped to the current region's bounds. Travel/unlock gating arrives in
// Phase 8 — until then every registered region is walkable.
using System.Collections.Generic;
using UnityEngine;

namespace CosmicBloom.World
{
    public class RegionManager : MonoBehaviour
    {
        public static RegionManager Instance { get; private set; }

        readonly Dictionary<string, Bounds> _regionBounds = new();

        public string CurrentRegionId { get; private set; } = "starlight_vale";

        public event System.Action<string> OnRegionChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterRegion(string regionId, Bounds worldBounds)
        {
            _regionBounds[regionId] = worldBounds;
        }

        public bool TryGetBounds(string regionId, out Bounds bounds) =>
            _regionBounds.TryGetValue(regionId, out bounds);

        public Bounds CurrentBounds =>
            _regionBounds.TryGetValue(CurrentRegionId, out var b) ? b : new Bounds(Vector3.zero, Vector3.one * 20f);

        /// <summary>Set the active region (travel). Unlock checks happen at the call site.</summary>
        public void SetCurrentRegion(string regionId)
        {
            if (regionId == CurrentRegionId) return;
            CurrentRegionId = regionId;
            OnRegionChanged?.Invoke(regionId);
        }

        /// <summary>Clamp a world point inside the current region's bounds (small margin).</summary>
        public Vector3 ClampToCurrentRegion(Vector3 p)
        {
            var b = CurrentBounds;
            const float margin = 0.3f;
            p.x = Mathf.Clamp(p.x, b.min.x + margin, b.max.x - margin);
            p.y = Mathf.Clamp(p.y, b.min.y + margin, b.max.y - margin);
            p.z = 0f;
            return p;
        }
    }
}
