// Cosmic Bloom — always-on HUD: coins, level+XP, day/night + day number,
// current region + restoration, active tool, selected crop. Buttons open
// quest log / map / pause and cycle tool & crop. All placeholder uGUI.
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
            // cycle among owned tools; selecting sets it for its action
            var owned = _state.Save.tools;
            if (owned.Count == 0) return;
            string current = _toolLabel.text.StartsWith("Tool") ? owned[0] : _toolLabel.text;
            int idx = Mathf.Max(0, owned.IndexOf(current));
            string next = owned[(idx + 1) % owned.Count];
            ToolService.SelectTool(_state, next);
            _toolLabel.text = next;
        }

        void CycleCrop()
        {
            string region = World.RegionManager.Instance != null
                ? World.RegionManager.Instance.CurrentRegionId : _state.Save.currentRegion;
            var options = new System.Collections.Generic.List<CropDef>(World.CropSelection.OptionsFor(region));
            if (options.Count == 0) return;
            string current = World.CropSelection.SelectedFor(_state, region);
            int idx = Mathf.Max(0, options.FindIndex(c => c.id == current));
            var next = options[(idx + 1) % options.Count];
            World.CropSelection.Select(region, next.id);
            _cropLabel.text = next.name;
        }

        void Update()
        {
            if (_state == null || _stats == null) return;
            var save = _state.Save;
            var time = TimeManager.Instance;
            string region = World.RegionManager.Instance != null
                ? World.RegionManager.Instance.CurrentRegionId : save.currentRegion;
            var regionDef = GameDatabase.Region(region);
            float restoration = _state.FarmFor(region).restoration;

            _stats.text =
                $"⛁ {save.coins}   Lv {save.level} ({save.xp}/{GameState.XpForLevel(save.level)}xp)   " +
                $"{(time != null && time.IsNight ? "☾ Night" : "☀ Day")} {time?.DayNumber ?? 1}   " +
                $"{regionDef?.name ?? region}: {restoration:F0}% restored";

            if (_toolLabel != null && _toolLabel.text == "Tool" && save.tools.Count > 0)
                _toolLabel.text = save.tools[0];
            if (_cropLabel != null && _cropLabel.text == "Crop")
                _cropLabel.text = GameDatabase.Crop(World.CropSelection.SelectedFor(_state, region))?.name ?? "Crop";
        }
    }
}
