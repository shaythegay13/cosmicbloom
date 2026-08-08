// Cosmic Bloom — P3-Phase 23: two-finger pinch-to-zoom.
//
// New Input System only (activeInputHandler=1; UnityEngine.Input throws here), so
// touches come off Touchscreen.current rather than Input.touches.
//
// This component owns GESTURE RECOGNITION only. Every actual size change goes
// through CameraFollow.SetOrthographicSize, which owns the [Min, Max] clamp and the
// re-clamp of camera position against the new zoom — so there is exactly one place
// that knows what a legal zoom and a legal camera position are. Don't write
// `cam.orthographicSize = ...` here or anywhere else.
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicBloom.World
{
    [RequireComponent(typeof(CameraFollow))]
    public class PinchZoom : MonoBehaviour
    {
        /// <summary>True while two fingers are down and driving the zoom. PlayerController
        /// reads this to stop a pinch from also being read as a tap-to-move.</summary>
        public static bool IsPinching { get; private set; }

        /// <summary>Minimum finger separation, in screen px, before a gesture is recognised.
        /// The zoom is a RATIO against the starting separation, so beginning at 3px would
        /// make a normal 200px spread a 66x change — one frame of finger jitter would
        /// slam the camera to a clamp bound. 60px is roughly two adjacent fingertips.</summary>
        public const float MinStartSeparationPx = 60f;

        /// <summary>How long after a move order a starting pinch may cancel it. The first
        /// finger of a pinch lands before the second, and PlayerController has already read
        /// it as a tap by then — without this cancel, every pinch walks the player somewhere.
        ///
        /// Note what the WINDOW does and does not buy, because it is easy to over-read
        /// (Phase23PinchZoomTests pins both halves). It does NOT save a walk-to-NPC from
        /// being lost to a pinch: when the first finger lands on open ground it is a real
        /// tap and PlayerController has already replaced that order with its own before
        /// this code runs. What it protects is the case where the first finger issued no
        /// tap at all — it came down on a HUD button and EventSystem swallowed it — where a
        /// blunt CancelMove would silently drop an order the gesture had nothing to do with.</summary>
        public const float TapCancelWindow = 0.35f;

        CameraFollow _follow;
        bool _active;
        float _startSeparation;
        float _startSize;

        void Awake() => _follow = GetComponent<CameraFollow>();

        void OnDisable() => EndGesture();

        void Update()
        {
            if (!TryReadPinchSeparation(out float separation)) { EndGesture(); return; }

            if (!_active)
            {
                // Wait for the fingers to be far enough apart to give a stable baseline
                // rather than beginning the gesture on whatever this frame happens to read.
                if (separation < MinStartSeparationPx) return;
                BeginGesture(separation);
                return;
            }

            ApplyPinch(separation);
        }

        /// <summary>Separation of the two active touches, or false when a pinch isn't
        /// possible this frame (no touchscreen, fewer than two fingers down).</summary>
        static bool TryReadPinchSeparation(out float separation)
        {
            separation = 0f;
            var screen = Touchscreen.current;
            if (screen == null) return false;

            Vector2 first = default;
            int found = 0;
            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                var pos = touch.position.ReadValue();
                if (found == 0) { first = pos; found = 1; continue; }
                separation = Vector2.Distance(first, pos);
                return true;
            }
            return false;
        }

        void BeginGesture(float separation)
        {
            if (_follow == null) _follow = GetComponent<CameraFollow>();
            if (_follow == null) return;

            _active = true;
            IsPinching = true;
            _startSeparation = separation;
            _startSize = _follow.OrthographicSize;

            // Undo the tap the first finger already fired (see TapCancelWindow).
            var player = _follow.target != null ? _follow.target.GetComponent<PlayerController>() : null;
            if (player == null) player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null) player.CancelRecentTapMove(TapCancelWindow);
        }

        void EndGesture()
        {
            _active = false;
            IsPinching = false;
        }

        /// <summary>Map the current finger separation onto a zoom level. Anchored on the
        /// separation the gesture STARTED at, not on the previous frame's — a per-frame
        /// delta accumulates float error over a long gesture and can't be undone by moving
        /// the fingers back, so the same pinch would land somewhere different each time.
        ///
        /// Fingers apart (separation grows) => smaller orthographic size => closer view,
        /// the standard mobile convention. Public so tests can drive the maths without a
        /// touchscreen; the gesture path above is exercised with real simulated touches.</summary>
        public float ApplyPinch(float separation)
        {
            if (_follow == null) _follow = GetComponent<CameraFollow>();
            if (_follow == null || separation <= Mathf.Epsilon || _startSeparation <= Mathf.Epsilon)
                return _follow != null ? _follow.OrthographicSize : CameraFollow.DefaultOrthographicSize;

            return _follow.SetOrthographicSize(_startSize * (_startSeparation / separation));
        }

        /// <summary>Start a gesture from a known separation. Tests only — the live path is
        /// Update reading Touchscreen.current.</summary>
        public void BeginGestureForTest(float separation) => BeginGesture(separation);

        /// <summary>Domain-reload hygiene: IsPinching is static, so a test (or a scene
        /// reload) that tears the camera down mid-gesture would otherwise leave every
        /// later tap suppressed.</summary>
        public static void ResetState() => IsPinching = false;
    }
}
