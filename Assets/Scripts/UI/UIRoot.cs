// Cosmic Bloom — code-built UI root: one screen-space canvas + an
// EventSystem wired for the new Input System (project is input-system-only).
// All game UI (dialogue, HUD, quest log, map, pause) hangs off this canvas.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CosmicBloom.UI
{
    public static class UIRoot
    {
        static Canvas _canvas;

        public static Canvas Canvas
        {
            get
            {
                if (_canvas != null) return _canvas;

                var go = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _canvas = go.GetComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);   // portrait phone
                scaler.matchWidthOrHeight = 0.5f;

                if (Object.FindAnyObjectByType<EventSystem>() == null)
                {
                    var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    Object.DontDestroyOnLoad(es);
                }
                return _canvas;
            }
        }

        /// <summary>Uniform placeholder text style (built-in LegacyRuntime font).</summary>
        public static Text MakeText(Transform parent, string name, int size,
                                    TextAnchor anchor = TextAnchor.MiddleCenter,
                                    FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Standard tappable button with label (44px+ touch target left to callers).</summary>
        public static Button MakeButton(Transform parent, string name, string label,
                                        System.Action onClick, int fontSize = 26)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.42f, 0.95f);
            var button = go.GetComponent<Button>();
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = MakeText(go.transform, "Label", fontSize);
            var rt = (RectTransform)text.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            text.text = label;
            return button;
        }

        /// <summary>Tests/domain hygiene.</summary>
        public static void Reset() => _canvas = null;
    }
}
