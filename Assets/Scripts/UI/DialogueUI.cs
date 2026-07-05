// Cosmic Bloom — minimal dialogue box, built in code (no scene/prefab UI to
// lose to domain reloads). Bottom-anchored panel: speaker name, body text,
// tap anywhere on it to close. Linear display — npcs.json dialogue has no
// branching (verified: keys are intro/idle/quest_complete only).
using System;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public static class DialogueUI
    {
        static GameObject _panel;
        static Text _nameText;
        static Text _bodyText;
        static Action _onClosed;

        public static bool IsOpen => _panel != null && _panel.activeSelf;

        /// <summary>What the box is showing right now (test/verification hook).</summary>
        public static string CurrentLine => IsOpen ? _bodyText.text : null;
        public static string CurrentSpeaker => IsOpen ? _nameText.text : null;

        public static void Show(string speaker, string line, Action onClosed = null)
        {
            EnsureBuilt();
            _nameText.text = speaker;
            _bodyText.text = line;
            _onClosed = onClosed;
            _panel.SetActive(true);
        }

        public static void Close()
        {
            if (!IsOpen) return;
            _panel.SetActive(false);
            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        static void EnsureBuilt()
        {
            if (_panel != null) return;
            var canvas = UIRoot.Canvas;

            _panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image), typeof(Button));
            _panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.03f, 0.02f);
            rt.anchorMax = new Vector2(0.97f, 0.30f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.07f, 0.05f, 0.16f, 0.92f);
            _panel.GetComponent<Button>().onClick.AddListener(Close);

            _nameText = UIRoot.MakeText(_panel.transform, "Name", 30, TextAnchor.UpperLeft, FontStyle.Bold);
            var nrt = (RectTransform)_nameText.transform;
            nrt.anchorMin = new Vector2(0.03f, 0.68f);
            nrt.anchorMax = new Vector2(0.97f, 0.97f);
            nrt.offsetMin = nrt.offsetMax = Vector2.zero;
            _nameText.color = new Color(1f, 0.88f, 0.5f);

            _bodyText = UIRoot.MakeText(_panel.transform, "Body", 24, TextAnchor.UpperLeft);
            var brt = (RectTransform)_bodyText.transform;
            brt.anchorMin = new Vector2(0.03f, 0.06f);
            brt.anchorMax = new Vector2(0.97f, 0.66f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
        }
    }
}
