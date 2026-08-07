// Cosmic Bloom — always-on HUD: coins, level+XP, day/night + day number,
// current region + restoration, active tool, selected crop. Buttons open
// quest log / map / pause and cycle tool & crop. All placeholder uGUI.
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public class HudUI : MonoBehaviour
    {
        GameState _state;

        public QuestLogUI QuestLog { get; private set; }
        public RegionMapUI Map { get; private set; }
        public PauseMenuUI Pause { get; private set; }
        public RanchUI Ranch { get; private set; }

        Text _stats;
        Text _toolLabel;
        Text _cropLabel;
        string _selectedToolId;   // real cycle position; never re-parsed from the label

        public static HudUI Create(GameState state, QuestEngine engine, World.RegionManager regions)
        {
            var go = new GameObject("HudUI");
            var hud = go.AddComponent<HudUI>();
            hud._state = state;
            hud.QuestLog = QuestLogUI.Create(engine);
            hud.Map = RegionMapUI.Create(state);
            hud.Pause = PauseMenuUI.Create();
            hud.Ranch = RanchUI.Create(state, regions);
            hud.Pause.OnRanch = () => hud.Ranch.Toggle();
            hud.Build();
            return hud;
        }

        void Build()
        {
            var canvas = UIRoot.Canvas;

            // top bar — stats
            var top = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            top.transform.SetParent(canvas.transform, false);
            var trt = (RectTransform)top.transform;
            trt.anchorMin = new Vector2(0f, 0.945f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            top.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.12f, 0.85f);

            _stats = UIRoot.MakeText(top.transform, "Stats", 26, TextAnchor.MiddleLeft);
            var srt = (RectTransform)_stats.transform;
            srt.anchorMin = new Vector2(0.02f, 0f);
            srt.anchorMax = new Vector2(0.98f, 1f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;

            // bottom bar — actions
            var bottom = new GameObject("BottomBar", typeof(RectTransform));
            bottom.transform.SetParent(canvas.transform, false);
            var brt = (RectTransform)bottom.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0.075f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;

            MakeBarButton(bottom.transform, 0, "Quests", () => QuestLog.Toggle());
            MakeBarButton(bottom.transform, 1, "Map", () => Map.Toggle());
            var tool = MakeBarButton(bottom.transform, 2, "Tool", CycleTool);
            _toolLabel = tool.GetComponentInChildren<Text>();
            var crop = MakeBarButton(bottom.transform, 3, "Crop", CycleCrop);
            _cropLabel = crop.GetComponentInChildren<Text>();
            MakeBarButton(bottom.transform, 4, "Menu", () => Pause.Toggle());

            // P3-Phase 21: these two labels used to be created reading "Tool"/"Crop" and
            // then overwritten inside the first Update, so their functional text was
            // never actually on screen for a frame the player could see — and the tool
            // one showed a raw snake_case id. Both now render their real, human-readable
            // value immediately, and every label is variable-length real data, so they
            // use best-fit sizing per the Phase 6 rule rather than a fixed font size.
            foreach (var label in new[] { _toolLabel, _cropLabel })
            {
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 10;
                label.resizeTextMaxSize = 22;
            }
            RefreshSelectorLabels();
        }

        static Button MakeBarButton(Transform bar, int slot, string label, System.Action onClick)
        {
            var b = UIRoot.MakeButton(bar, label, label, onClick);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0.01f + slot * 0.198f, 0.08f);
            rt.anchorMax = new Vector2(0.192f + slot * 0.198f, 0.95f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return b;
        }

        void CycleTool()
        {
            // cycle among owned tools; selecting sets it for its action.
            // The index comes from _selectedToolId, NOT from parsing the label back out
            // — the old label round-trip broke the moment the label stopped being the
            // bare tool id, and silently restarted the cycle at owned[0] every tap.
            var owned = _state.Save.tools;
            if (owned.Count == 0) return;
            int idx = Mathf.Max(0, owned.IndexOf(_selectedToolId ?? owned[0]));
            string next = owned[(idx + 1) % owned.Count];
            ToolService.SelectTool(_state, next);
            _selectedToolId = next;
            RefreshSelectorLabels();
        }

        void CycleCrop()
        {
            var options = new System.Collections.Generic.List<CropDef>(World.CropSelection.OptionsFor(CurrentRegion));
            if (options.Count == 0) return;
            string current = World.CropSelection.SelectedFor(_state, CurrentRegion);
            int idx = Mathf.Max(0, options.FindIndex(c => c.id == current));
            var next = options[(idx + 1) % options.Count];
            World.CropSelection.Select(CurrentRegion, next.id);
            RefreshSelectorLabels();
        }

        string CurrentRegion => World.RegionManager.Instance != null
            ? World.RegionManager.Instance.CurrentRegionId : _state.Save.currentRegion;

        /// <summary>Display name for a tool id, from tools.json. Falls back to the raw id
        /// only if data has no entry — the player should never see snake_case.</summary>
        static string ToolName(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var def = GameDatabase.Tools.FirstOrDefault(t => t.id == id);
            return def?.name ?? id;
        }

        /// <summary>P3-Phase 21: both selectors read as controls ("Tool: Cosmic Hoe ⇄")
        /// rather than status readouts, and always show a real display name. Public for
        /// tests and for callers that change selection outside these buttons.</summary>
        public void RefreshSelectorLabels()
        {
            if (_state == null) return;

            if (_toolLabel != null)
            {
                if (_selectedToolId == null || !_state.Save.tools.Contains(_selectedToolId))
                    _selectedToolId = _state.Save.tools.Count > 0 ? _state.Save.tools[0] : null;
                _toolLabel.text = _selectedToolId != null
                    ? $"Tool: {ToolName(_selectedToolId)} ⇄"
                    : "Tool: none ⇄";
            }

            if (_cropLabel != null)
            {
                var crop = GameDatabase.Crop(World.CropSelection.SelectedFor(_state, CurrentRegion));
                _cropLabel.text = crop != null ? $"Crop: {crop.name} ⇄" : "Crop: none ⇄";
            }
        }

        void Update()
        {
            if (_state == null || _stats == null) return;
            var save = _state.Save;
            var time = TimeManager.Instance;
            string region = CurrentRegion;
            var regionDef = GameDatabase.Region(region);
            float rewilded = _state.FarmFor(region).restoration;

            // P3-Phase 21: this used to read "% restored", the same word LANDMARK
            // restoration uses — two unrelated mechanics sharing one label. This meter is
            // FarmState.restoration, driven by harvesting native crops; landmarks are a
            // separate coin-paid count (FarmState.landmarksRestored, surfaced in the Star
            // Map). "Rewilded" is the GDD's own word for the farm→rewild loop, so the
            // rename goes here and "restored" stays with landmarks.
            _stats.text =
                $"⛁ {save.coins}   Lv {save.level} ({save.xp}/{GameState.XpForLevel(save.level)}xp)   " +
                $"{(time != null && time.IsNight ? "☾ Night" : "☀ Day")} {time?.DayNumber ?? 1}   " +
                $"{regionDef?.name ?? region}: {rewilded:F0}% rewilded";

            RefreshSelectorLabels();
        }
    }
}
