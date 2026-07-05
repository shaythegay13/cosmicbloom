// Cosmic Bloom — quest log: active quests with current-step progress, then
// completed count. Steps blocked on out-of-scope systems show an explicit
// "[skip]" button (see QuestEngine DECISION) rather than silently passing.
using System.Text;
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public class QuestLogUI : MonoBehaviour
    {
        QuestEngine _engine;
        GameObject _panel;
        Text _body;
        Button _skipButton;
        Text _skipLabel;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public static QuestLogUI Create(QuestEngine engine)
        {
            var go = new GameObject("QuestLogUI");
            var ui = go.AddComponent<QuestLogUI>();
            ui._engine = engine;
            engine.OnProgressChanged += ui.Refresh;
            engine.OnQuestStarted += _ => ui.Refresh();
            engine.OnQuestCompleted += _ => ui.Refresh();
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

            _panel = new GameObject("QuestPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.05f, 0.32f);
            rt.anchorMax = new Vector2(0.95f, 0.88f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.95f);

            var title = UIRoot.MakeText(_panel.transform, "Title", 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = new Vector2(0f, 0.90f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            title.text = "Quest Log";

            _body = UIRoot.MakeText(_panel.transform, "Body", 24, TextAnchor.UpperLeft);
            var brt = (RectTransform)_body.transform;
            brt.anchorMin = new Vector2(0.04f, 0.14f);
            brt.anchorMax = new Vector2(0.96f, 0.89f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;

            _skipButton = UIRoot.MakeButton(_panel.transform, "SkipPending", "", () =>
            {
                foreach (var id in _engine.ActiveQuests())
                    if (QuestEngine.IsPendingSystemStep(_engine.CurrentStep(id)))
                    {
                        _engine.DebugSkipStep(id);
                        break;
                    }
                Refresh();
            });
            var srt = (RectTransform)_skipButton.transform;
            srt.anchorMin = new Vector2(0.15f, 0.02f);
            srt.anchorMax = new Vector2(0.85f, 0.115f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            _skipLabel = _skipButton.GetComponentInChildren<Text>();

            var close = UIRoot.MakeButton(_panel.transform, "Close", "X", () => _panel.SetActive(false));
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.90f, 0.90f);
            crt.anchorMax = new Vector2(0.99f, 0.99f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;

            _panel.SetActive(false);
        }

        public void Refresh()
        {
            if (_panel == null || !_panel.activeSelf || _engine == null) return;

            var sb = new StringBuilder();
            bool anyPendingSkip = false;

            foreach (var id in _engine.ActiveQuests())
            {
                var def = GameDatabase.Quest(id);
                if (def == null) continue;
                var step = _engine.CurrentStep(id);
                sb.Append("<b>").Append(def.title).Append("</b>  <i>(Act ").Append(def.act).Append(")</i>\n");
                sb.Append(def.summary).Append('\n');
                if (step != null)
                {
                    sb.Append("  ▸ step ").Append(_engine.StepIndex(id) + 1).Append('/')
                      .Append(def.steps.Count).Append(": ").Append(DescribeStep(step))
                      .Append("  [").Append(_engine.StepProgress(id)).Append('/')
                      .Append(Mathf.Max(1, step.type == "talk_to" ? (step.npcs?.Count ?? 1) : step.count)).Append("]\n");
                    if (QuestEngine.IsPendingSystemStep(step)) anyPendingSkip = true;
                }
                sb.Append('\n');
            }

            var save = _engine.State.Save;
            sb.Append("— completed: ").Append(save.questsCompleted.Count).Append(" / 60");
            _body.text = sb.ToString();

            _skipButton.gameObject.SetActive(anyPendingSkip);
            if (anyPendingSkip)
                _skipLabel.text = "Skip step (system pending — overnight stub)";
        }

        static string DescribeStep(QuestStep s)
        {
            var label = s.type.Replace('_', ' ');
            if (!string.IsNullOrEmpty(s.crop)) label += $" {s.crop}";
            if (!string.IsNullOrEmpty(s.item)) label += $" {s.item}";
            if (!string.IsNullOrEmpty(s.landmark)) label += $" {s.landmark}";
            if (!string.IsNullOrEmpty(s.region)) label += $" {s.region}";
            if (!string.IsNullOrEmpty(s.animal)) label += $" {s.animal}";
            if (!string.IsNullOrEmpty(s.target)) label += $" {s.target}";
            if (!string.IsNullOrEmpty(s.@event)) label += $" {s.@event}";
            if (s.threshold > 0) label += $" ≥{s.threshold}";
            if (s.kinds > 0) label += $" ({s.kinds} kinds)";
            return label;
        }
    }
}
