// Cosmic Bloom — ranch panel: the 15 animals from animals.json with cost,
// level requirement and owned state; tap to buy (data-driven, no per-animal
// code). Opened from the pause/menu panel.
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.World;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public class RanchUI : MonoBehaviour
    {
        GameState _state;
        RegionManager _regions;
        GameObject _panel;
        readonly System.Collections.Generic.List<(AnimalDef def, Text label)> _entries = new();

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public static RanchUI Create(GameState state, RegionManager regions)
        {
            var go = new GameObject("RanchUI");
            var ui = go.AddComponent<RanchUI>();
            ui._state = state;
            ui._regions = regions;
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

            _panel = new GameObject("RanchPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.05f, 0.10f);
            rt.anchorMax = new Vector2(0.95f, 0.92f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.96f);

            var title = UIRoot.MakeText(_panel.transform, "Title", 32, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 0.94f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            title.text = "Cosmic Ranch";

            GameDatabase.LoadAll();
            var animals = GameDatabase.Animals;
            for (int i = 0; i < animals.Count; i++)
            {
                var def = animals[i];
                var button = UIRoot.MakeButton(_panel.transform, $"Animal_{def.id}", "",
                    () => { AnimalSpawner.TryBuy(_state, _regions, def.id); Refresh(); }, 18);
                var brt = (RectTransform)button.transform;
                float top = 0.925f - i * 0.0585f;
                brt.anchorMin = new Vector2(0.04f, top - 0.052f);
                brt.anchorMax = new Vector2(0.96f, top);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                _entries.Add((def, label));
            }

            var close = UIRoot.MakeButton(_panel.transform, "Close", "X", () => _panel.SetActive(false));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.90f, 0.945f);
            crt.anchorMax = new Vector2(0.99f, 0.998f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            _panel.SetActive(false);
        }

        public void Refresh()
        {
            foreach (var (def, label) in _entries)
            {
                bool owned = _state.Save.animals.Contains(def.id);
                label.text = owned
                    ? $"  ✓ {def.name} ({def.kind}) — produces {def.produce ?? "joy"}"
                    : $"  {def.name} ({def.kind}) — {def.cost}c, Lv {def.unlockLevel}" +
                      (_state.Save.level >= def.unlockLevel && _state.Save.coins >= def.cost ? "  [tap to buy]" : "");
            }
        }
    }
}
