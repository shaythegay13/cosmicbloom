// Cosmic Bloom — region map / travel screen: all five regions from
// regions.json with locked/unlocked state; tap an unlocked one to travel,
// tap a locked one to attempt the data-driven unlock (coins + level).
using System.Linq;
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
                // P3-Phase 21: each row now carries its region's landmark lines, so the
                // text is variable-length real data (1 to 4 lines). Per the Phase 6 rule,
                // that means best-fit sizing — a fixed size would silently truncate the
                // landmark list rather than showing an overlap.
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 9;
                label.resizeTextMaxSize = 24;
                label.alignment = TextAnchor.MiddleLeft;
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
                float rewilded = _state.FarmFor(def.id).restoration;

                // "rewilded" here, "restored" on the landmark lines below — the two used
                // to share the word "restored" for unrelated mechanics (P3-Phase 21).
                string header = unlocked
                    ? $"{def.name} — {rewilded:F0}% rewilded{(current ? "   ◂ you are here" : "   (tap to travel)")}"
                    : $"🔒 {def.name} — unlock: {def.unlockCoins} coins, Lv {def.unlockLevel}" +
                      (RegionUnlockService.MeetsLevel(_state, def) && RegionUnlockService.CanAfford(_state, def)
                        ? "   (tap to unlock!)" : "");

                label.text = header + LandmarkLines(def);
            }
        }

        /// <summary>P3-Phase 21: a landmark's restoreCost, what it unlocks, and whether
        /// it's done were previously discoverable ONLY by physically walking to it and
        /// tapping — there was no way to plan for a 900-coin restore you hadn't found.
        /// The Star Map already lists every region, so the landmark ledger hangs off it.
        /// All values read from regions.json + FarmState.landmarksRestored.</summary>
        string LandmarkLines(RegionDef def)
        {
            if (def.landmarks == null || def.landmarks.Count == 0) return string.Empty;

            var done = _state.FarmFor(def.id).landmarksRestored;
            var sb = new System.Text.StringBuilder();
            sb.Append($"\n   Landmarks restored {def.landmarks.Count(l => done.Contains(l.id))}/{def.landmarks.Count}");
            foreach (var lm in def.landmarks)
            {
                bool restored = done.Contains(lm.id);
                sb.Append(restored
                    ? $"\n   ✓ {lm.name} — restored → {Pretty(lm.unlocks)}"
                    : $"\n   ◦ {lm.name} — {lm.restoreCost} coins → {Pretty(lm.unlocks)}");
            }
            return sb.ToString();
        }

        /// <summary>regions.json stores `unlocks` as a snake_case key; show it as words.</summary>
        static string Pretty(string unlocks) =>
            string.IsNullOrEmpty(unlocks) ? "—" : unlocks.Replace('_', ' ');
    }
}
