// Cosmic Bloom — one tile-sized plot view + tap interaction, generic across
// all regions. Created by FarmGrid; state lives in the save's per-region
// FarmState.plots via FarmingSystem (no local copies).
// Replaces the Phase-0 single-plot proof of concept (prefab retired).
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.Farming
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class FarmPlot : MonoBehaviour, World.IInteractable
    {
        public string regionId;
        public int gridX;
        public int gridY;

        static readonly Color EmptyColor = new Color32(0x55, 0x55, 0x55, 0x60);   // faint until tilled
        static readonly Color TilledColor = new Color32(0x8B, 0x69, 0x14, 0xFF);
        static readonly Color WaterAccent = new Color32(0x44, 0xAA, 0xFF, 0xFF);
        static readonly Color ReadyAccent = new Color32(0xFF, 0xE0, 0x66, 0xFF);

        SpriteRenderer _sr;
        FarmingSystem _farming;
        GameState _state;

        public Vector3 InteractPosition => transform.position;

        public static FarmPlot Create(Transform parent, Grid grid, string regionId, int x, int y,
                                      FarmingSystem farming, GameState state)
        {
            var go = new GameObject($"Plot_{x}_{y}");
            go.transform.SetParent(parent, false);
            go.transform.position = grid.GetCellCenterWorld(new Vector3Int(x, y, 0));

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = World.ProceduralSprites.Square(32, 6);
            sr.sortingOrder = 5;
            var mat = Resources.Load<Material>("Materials/FarmPlotSprite");
            if (mat != null) sr.sharedMaterial = mat;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one * 0.95f;

            var plot = go.AddComponent<FarmPlot>();
            plot.regionId = regionId;
            plot.gridX = x;
            plot.gridY = y;
            plot.Bind(farming, state);
            return plot;
        }

        void Awake() => _sr = GetComponent<SpriteRenderer>();

        public void Bind(FarmingSystem farming, GameState state)
        {
            if (_farming != null) _farming.OnAction -= OnFarmAction;
            _farming = farming;
            _state = state;
            if (_farming != null) _farming.OnAction += OnFarmAction;
            RefreshVisual();
        }

        void OnDestroy()
        {
            if (_farming != null) _farming.OnAction -= OnFarmAction;
        }

        void OnFarmAction(string action, int x, int y)
        {
            if (x == gridX && y == gridY) RefreshVisual();
        }

        void LateUpdate() => RefreshVisual();   // growth advances via FastForward timers

        public PlotState LivePlot()
        {
            if (_state == null) return null;
            return _state.FarmFor(regionId).plots.TryGetValue($"{gridX},{gridY}", out var p) ? p : null;
        }

        /// <summary>Tap: perform this plot's contextual action (till/plant/water/harvest).</summary>
        public void Interact(World.PlayerController player)
        {
            if (_farming == null || _state == null) return;
            if (World.RegionManager.Instance != null &&
                World.RegionManager.Instance.CurrentRegionId != regionId) return;   // must be in-region

            string action = _farming.ActionFor(gridX, gridY);
            bool ok = action switch
            {
                "till" => _farming.Till(gridX, gridY),
                "plant" => _farming.Plant(gridX, gridY, World.CropSelection.SelectedFor(_state, regionId)),
                "water" => _farming.Water(gridX, gridY),
                "harvest" => _farming.Harvest(gridX, gridY),
                _ => false,
            };
            Debug.Log($"[FarmPlot] {regionId}({gridX},{gridY}) action={action ?? "none"} ok={ok}");
        }

        public void RefreshVisual()
        {
            if (_sr == null) return;
            var plot = LivePlot();
            if (plot == null || !plot.tilled)
            {
                _sr.color = EmptyColor;
                transform.localScale = Vector3.one;
                return;
            }
            if (plot.crop == null)
            {
                _sr.color = TilledColor;
                transform.localScale = Vector3.one;
                return;
            }

            var def = GameDatabase.Crop(plot.crop.id);
            Color crop = def != null ? World.ProceduralSprites.ParseHex(def.color) : Color.magenta;
            float stageT = def != null && def.stages > 1 ? plot.crop.stage / (float)(def.stages - 1) : 1f;

            _sr.color = Color.Lerp(TilledColor, crop, 0.35f + stageT * 0.65f);
            transform.localScale = Vector3.one * (0.85f + stageT * 0.15f);

            if (plot.crop.needsWater)
                _sr.color = Color.Lerp(_sr.color, WaterAccent, 0.35f);
            else if (def != null && plot.crop.stage >= def.stages - 1)
                _sr.color = Color.Lerp(_sr.color, ReadyAccent, 0.25f);
        }
    }
}
