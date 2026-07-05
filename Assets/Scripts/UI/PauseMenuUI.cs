// Cosmic Bloom — pause menu: pause/resume (timeScale) + manual save.
using CosmicBloom.Save;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        GameObject _panel;
        Text _status;

        /// <summary>Optional extra entry (the ranch panel) wired by HudUI.</summary>
        public System.Action OnRanch;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public static PauseMenuUI Create()
        {
            var go = new GameObject("PauseMenuUI");
            return go.AddComponent<PauseMenuUI>();
        }

        public void Toggle()
        {
            EnsureBuilt();
            bool opening = !_panel.activeSelf;
            _panel.SetActive(opening);
            Time.timeScale = opening ? 0f : 1f;
            if (opening) _status.text = "Paused";
        }

        void EnsureBuilt()
        {
            if (_panel != null) return;
            var canvas = UIRoot.Canvas;

            _panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.15f, 0.30f);
            rt.anchorMax = new Vector2(0.85f, 0.70f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.14f, 0.97f);

            _status = UIRoot.MakeText(_panel.transform, "Status", 34, TextAnchor.MiddleCenter, FontStyle.Bold);
            var srt = (RectTransform)_status.transform;
            srt.anchorMin = new Vector2(0f, 0.75f);
            srt.anchorMax = new Vector2(1f, 0.98f);
            srt.offsetMin = srt.offsetMax = Vector2.zero;
            _status.text = "Paused";

            var resume = UIRoot.MakeButton(_panel.transform, "Resume", "Resume", Toggle);
            var rrt = (RectTransform)resume.transform;
            rrt.anchorMin = new Vector2(0.12f, 0.46f);
            rrt.anchorMax = new Vector2(0.88f, 0.66f);
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;

            var save = UIRoot.MakeButton(_panel.transform, "Save", "Save game", () =>
            {
                if (SaveManager.Instance != null)
                {
                    SaveManager.Instance.WriteLocal();
                    _status.text = "Saved ✓";
                }
            });
            var savRt = (RectTransform)save.transform;
            savRt.anchorMin = new Vector2(0.12f, 0.24f);
            savRt.anchorMax = new Vector2(0.88f, 0.40f);
            savRt.offsetMin = savRt.offsetMax = Vector2.zero;

            var ranch = UIRoot.MakeButton(_panel.transform, "Ranch", "Cosmic Ranch", () =>
            {
                Toggle();          // unpause first so the ranch is usable
                OnRanch?.Invoke();
            });
            var ranchRt = (RectTransform)ranch.transform;
            ranchRt.anchorMin = new Vector2(0.12f, 0.04f);
            ranchRt.anchorMax = new Vector2(0.88f, 0.20f);
            ranchRt.offsetMin = ranchRt.offsetMax = Vector2.zero;

            _panel.SetActive(false);
        }
    }
}
