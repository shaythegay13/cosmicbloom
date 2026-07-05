// Cosmic Bloom — region map / travel screen: all five regions from
// regions.json with locked/unlocked state; tap an unlocked one to travel,
// tap a locked one to attempt the data-driven unlock (coins + level).
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public class RegionMapUI : MonoBehaviour
    {
        GameState _state;
        GameObject _panel;
        readonly System.Collections.Generic.List<(RegionDef def, Text label)> _entries = new();

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public static RegionMapUI Create(GameState state)
        {
            var go = new GameObject("RegionMapUI");
            var ui = go.AddComponent<RegionMapUI>();
            ui._state = state;
            return ui;
        }

        public void Toggle()
        {
            EnsureBuilt();
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf) Refresh();
        }

        void EnsureBuilt()
        {
            if (_panel != null) return;
            var canvas = UIRoot.Canvas;

            _panel = new GameObject("MapPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.05f, 0.18f);
            rt.anchorMax = new Vector2(0.95f, 0.90f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.96f);

            var title = UIRoot.MakeText(_panel.transform, "Title", 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 0.91f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            title.text = "Star Map";

            GameDatabase.LoadAll();
            for (int i = 0; i < GameDatabase.Regions.Count; i++)
            {
                var def = GameDatabase.Regions[i];
                var button = UIRoot.MakeButton(_panel.transform, $"Region_{def.id}", "", () => OnTapRegion(def));
                var brt = (RectTransform)button.transform;
                float top = 0.88f - i * 0.165f;
                brt.anchorMin = new Vector2(0.06f, top - 0.145f);
                brt.anchorMax = new Vector2(0.94f, top);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                button.GetComponent<Image>().color = ProceduralSprites.ParseHex(def.groundTiers[2]) * 0.55f;
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 24;
                _entries.Add((def, label));
            }

            var close = UIRoot.MakeButton(_panel.transform, "Close", "X", () => _panel.SetActive(false));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.90f, 0.92f);
            crt.anchorMax = new Vector2(0.99f, 0.995f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            _panel.SetActive(false);
        }

        void OnTapRegion(RegionDef def)
        {
            if (RegionUnlockService.IsUnlocked(_state, def.id))
            {
                if (RegionUnlockService.TravelTo(_state, def.id)) _panel.SetActive(false);
            }
            else
            {
                RegionUnlockService.TryUnlock(_state, def.id);
            }
            Refresh();
        }

        public void Refresh()
        {
            foreach (var (def, label) in _entries)
            {
                bool unlocked = RegionUnlockService.IsUnlocked(_state, def.id);
                bool current = RegionManager.Instance != null && RegionManager.Instance.CurrentRegionId == def.id;
                float restoration = _state.FarmFor(def.id).restoration;

                label.text = unlocked
                    ? $"{def.name} — {restoration:F0}% restored{(current ? "   ◂ you are here" : "   (tap to travel)")}"
                    : $"🔒 {def.name} — unlock: {def.unlockCoins} coins, Lv {def.unlockLevel}" +
                      (RegionUnlockService.MeetsLevel(_state, def) && RegionUnlockService.CanAfford(_state, def)
                        ? "   (tap to unlock!)" : "");
            }
        }
    }
}
