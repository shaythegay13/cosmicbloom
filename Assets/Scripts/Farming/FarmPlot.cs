using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Save;
using UnityEngine;

namespace CosmicBloom.Farming
{
    /// <summary>
    /// One tile-sized plot view synced to FarmingSystem PlotState at gridX/gridY.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FarmPlot : MonoBehaviour
    {
        public int gridX = 10;
        public int gridY = 10;

        static readonly Color EmptyColor = new Color32(0x55, 0x55, 0x55, 0xFF);
        static readonly Color TilledColor = new Color32(0x8B, 0x69, 0x14, 0xFF);
        static readonly Color WaterAccent = new Color32(0x44, 0xAA, 0xFF, 0xFF);
        static readonly Color ReadyAccent = new Color32(0xFF, 0xE0, 0x66, 0xFF);

        SpriteRenderer _sr;
        FarmingSystem _farming;
        static Sprite _sprite;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = GetTileSprite();
            _sr.sortingOrder = 5;
            var mat = Resources.Load<Material>("Materials/FarmPlotSprite");
            if (mat != null) _sr.sharedMaterial = mat;
            SnapToGrid();
        }

        void OnEnable()
        {
            if (SaveManager.Instance != null)
            {
                Bind(SaveManager.Instance.Farming);
            }
        }

        void OnDisable()
        {
            if (_farming != null) _farming.OnAction -= OnFarmAction;
        }

        public void Bind(FarmingSystem farming)
        {
            if (_farming != null) _farming.OnAction -= OnFarmAction;
            _farming = farming;
            if (_farming != null) _farming.OnAction += OnFarmAction;
            RefreshVisual();
        }

        void OnFarmAction(string action, int x, int y)
        {
            if (x == gridX && y == gridY) RefreshVisual();
        }

        void LateUpdate()
        {
            if (_farming == null && SaveManager.Instance != null)
                Bind(SaveManager.Instance.Farming);
            RefreshVisual();
        }

        public void SnapToGrid()
        {
            var grid = GameObject.Find("StarlightVale")?.GetComponent<Grid>();
            if (grid == null) return;
            var cell = new Vector3Int(gridX, gridY, 0);
            transform.position = grid.GetCellCenterWorld(cell);
        }

        public PlotState LivePlot() => _farming?.PlotAt(gridX, gridY);

        public void RefreshVisual()
        {
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
            Color crop = def != null ? ParseHex(def.color) : Color.magenta;
            float stageT = def != null && def.stages > 1
                ? plot.crop.stage / (float)(def.stages - 1)
                : 1f;

            _sr.color = Color.Lerp(TilledColor, crop, 0.35f + stageT * 0.65f);
            transform.localScale = Vector3.one * (0.85f + stageT * 0.15f);

            if (plot.crop.needsWater)
                _sr.color = Color.Lerp(_sr.color, WaterAccent, 0.35f);
            else if (def != null && plot.crop.stage >= def.stages - 1)
                _sr.color = Color.Lerp(_sr.color, ReadyAccent, 0.25f);
        }

        static Sprite GetTileSprite()
        {
            if (_sprite != null) return _sprite;
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 6; y < 26; y++)
                for (int x = 6; x < 26; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            return _sprite;
        }

        static Color ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            return new Color32(r, g, b, 255);
        }
    }
}
