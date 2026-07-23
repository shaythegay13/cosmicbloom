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
        // Tilled-soil color by ground tier (P3-Phase 17 live-verify finding A): a wild-
        // tier plot must visibly read as poorer dirt, not just carry a hidden lower
        // soilHealth number the player only discovers by losing a crop to it.
        static readonly Color LushTilledColor = new Color32(0x8B, 0x69, 0x14, 0xFF);   // rich soil (unchanged original color)
        static readonly Color MidTilledColor = new Color32(0x7A, 0x67, 0x46, 0xFF);    // decent, desaturated
        static readonly Color WildTilledColor = new Color32(0x6E, 0x62, 0x55, 0xFF);   // poor, dull gray-brown
        static readonly Color WaterAccent = new Color32(0x44, 0xAA, 0xFF, 0xFF);
        static readonly Color ReadyAccent = new Color32(0xFF, 0xE0, 0x66, 0xFF);

        const int BaseSortingOrder = 5;    // below the player (10) — bare dirt has no height
        // Above the player (10), below FX particles (20). A growing crop has visible
        // height and shouldn't be fully hidden by someone standing on the tile to
        // interact with it — the tile itself has no height and stays below the player
        // (P3-Phase 17 live-verify finding B).
        const int CropSortingOrder = 11;

        SpriteRenderer _sr;
        FarmingSystem _farming;
        GameState _state;

        /// <summary>Test/inspection access — the plot's current tint.</summary>
        public Color CurrentColor => _sr != null ? _sr.color : Color.clear;
        /// <summary>Test/inspection access — the plot's current sprite sorting order.</summary>
        public int CurrentSortingOrder => _sr != null ? _sr.sortingOrder : 0;

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
            var mat = World.ProceduralSprites.LitMaterial;
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
            if (_farming != null) { _farming.OnAction -= OnFarmAction; _farming.OnReward -= OnFarmReward; }
            _farming = farming;
            _state = state;
            if (_farming != null) { _farming.OnAction += OnFarmAction; _farming.OnReward += OnFarmReward; }
            RefreshVisual();
        }

        void OnDestroy()
        {
            if (_farming != null) { _farming.OnAction -= OnFarmAction; _farming.OnReward -= OnFarmReward; }
        }

        void OnFarmAction(string action, int x, int y)
        {
            if (x != gridX || y != gridY) return;
            RefreshVisual();
            FarmActionEffects.Spawn(action, transform.position);
        }

        /// <summary>Floating "+coins/+xp" popup at this plot on harvest (P3-Phase 17
        /// live-verify finding E) — in addition to the HUD's running totals, reward
        /// feedback should happen where the action occurred.</summary>
        void OnFarmReward(int x, int y, int coins, int xp)
        {
            if (x != gridX || y != gridY) return;
            RewardPopup.Spawn(transform.position, coins, xp);
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
            int done = action switch
            {
                "till" or "water" or "harvest" => ToolService.Perform(_farming, _state, action, gridX, gridY),
                "plant" => ToolService.Perform(_farming, _state, action, gridX, gridY,
                                               World.CropSelection.SelectedForPlanting(_state, regionId)),
                _ => 0,
            };
            Debug.Log($"[FarmPlot] {regionId}({gridX},{gridY}) action={action ?? "none"} cells={done}");
        }

        static readonly Sprite PlotSquare = World.ProceduralSprites.Square(32, 6);

        static Color TilledColorFor(int tier) => tier switch
        {
            2 => LushTilledColor,
            1 => MidTilledColor,
            _ => WildTilledColor,
        };

        public void RefreshVisual()
        {
            if (_sr == null) return;
            var plot = LivePlot();
            _sr.sortingOrder = (plot != null && plot.crop != null) ? CropSortingOrder : BaseSortingOrder;

            if (plot == null || !plot.tilled)
            {
                _sr.sprite = PlotSquare;
                _sr.color = EmptyColor;
                transform.localScale = Vector3.one;
                return;
            }

            int tier = World.RegionWorldBuilder.TierAt(gridX, gridY);

            if (plot.crop == null)
            {
                _sr.sprite = PlotSquare;
                _sr.color = TilledColorFor(tier);
                transform.localScale = Vector3.one;
                return;
            }

            var def = GameDatabase.Crop(plot.crop.id);
            float stageT = def != null && def.stages > 1 ? plot.crop.stage / (float)(def.stages - 1) : 1f;
            transform.localScale = Vector3.one * (0.85f + stageT * 0.15f);

            // Real generated art if it loaded (Tools/art/build-cosmic.mjs output); the
            // sprite already carries the crop's color, so start from white instead of
            // the placeholder's crop-color lerp. Falls back to the tinted square below
            // when nothing was generated for this crop/stage (or archetype coverage
            // gaps in the generator).
            var generated = World.GeneratedSprites.Crop(plot.crop.id, plot.crop.stage);
            if (generated != null)
            {
                _sr.sprite = generated;
                _sr.color = Color.white;
                if (plot.crop.needsWater)
                    _sr.color = Color.Lerp(_sr.color, WaterAccent, 0.30f);
                else if (def != null && plot.crop.stage >= def.stages - 1)
                    _sr.color = Color.Lerp(_sr.color, ReadyAccent, 0.20f);
                return;
            }

            _sr.sprite = PlotSquare;
            Color crop = def != null ? World.ProceduralSprites.ParseHex(def.color) : Color.magenta;
            _sr.color = Color.Lerp(TilledColorFor(tier), crop, 0.35f + stageT * 0.65f);

            if (plot.crop.needsWater)
                _sr.color = Color.Lerp(_sr.color, WaterAccent, 0.35f);
            else if (def != null && plot.crop.stage >= def.stages - 1)
                _sr.color = Color.Lerp(_sr.color, ReadyAccent, 0.25f);
        }
    }
}
