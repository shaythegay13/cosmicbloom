// P3-Phase 23 verification — player-facing pinch-to-zoom.
//
// Three things have to be true at once, and each has its own class below:
//
//   Phase23PinchZoomTests (InputTestFixture) — the GESTURE. Real simulated
//     two-finger Touchscreen input through the Input System, not a direct call to
//     SetOrthographicSize. Covers the convention (fingers apart = closer view), the
//     [Min, Max] clamp, the pinch-must-not-also-walk-the-player interaction with
//     PlayerController's tap handler, and the UI-immunity claim measured under a
//     real gesture rather than read off UIRoot's setup.
//
//   Phase23ZoomClampTests — the CLAMP. CameraFollow.ClampedFocus derives its legal
//     box from the LIVE orthographic size, so every zoom level has a different box.
//     P3-Phase 20 fixed a bug where SnapTo bypassed the shared clamp entirely; the
//     zoom-shaped version of that bug is a clamp computed once for ortho 14 and then
//     reused, so these tests assert the box actually MOVES with zoom and that both
//     callers (LateUpdate and SnapTo) plus SetOrthographicSize agree at every level.
//
//   Phase23ZoomCaptureTests — the EVIDENCE (render split). The min-zoom candidate
//     sweep that CameraFollow.MinOrthographicSize is derived from, plus captures at
//     the shipped min and max with the HUD in frame.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicBloom.Core;
using CosmicBloom.Data;
using CosmicBloom.Farming;
using CosmicBloom.UI;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CosmicBloom.Tests
{
    /// <summary>Shared world scaffolding: the five regions, a farming system, a player at
    /// the Vale centre, and a portrait camera carrying CameraFollow + PinchZoom.</summary>
    static class Phase23Rig
    {
        public const int W = 720, H = 1280;
        public const float Aspect = (float)W / H;
        public const string Vale = "starlight_vale";

        /// <summary>Source art is 64px at PPU 64, so one sprite is exactly 1 world unit —
        /// screen-px-per-unit is the whole legibility story (Phase20CameraFramingTests).</summary>
        public const float SpriteSourcePx = 64f;

        public static float PxPerUnit(float orthoSize) => H / (2f * orthoSize);

        public static (Camera cam, CameraFollow follow, PinchZoom pinch) MakeCamera(
            Transform target, float size)
        {
            var go = new GameObject("PinchCam") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.aspect = Aspect;   // batchmode: don't inherit the RT/desktop aspect
            cam.backgroundColor = new Color(0.05f, 0.04f, 0.10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);

            var follow = go.AddComponent<CameraFollow>();
            follow.target = target;
            var pinch = go.AddComponent<PinchZoom>();
            follow.SnapTo(target.position);
            return (cam, follow, pinch);
        }

        /// <summary>The camera's world-space view rect, honouring the explicit test aspect.</summary>
        public static Rect ViewRect(Camera cam)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            var p = cam.transform.position;
            return new Rect(p.x - halfW, p.y - halfH, halfW * 2f, halfH * 2f);
        }

        public static void AssertViewInsideRegion(Camera cam, Bounds bounds, string context)
        {
            var rect = ViewRect(cam);
            Assert.GreaterOrEqual(rect.xMin, bounds.min.x - 0.01f, $"view left edge inside region — {context}");
            Assert.LessOrEqual(rect.xMax, bounds.max.x + 0.01f, $"view right edge inside region — {context}");
            Assert.GreaterOrEqual(rect.yMin, bounds.min.y - 0.01f, $"view bottom edge inside region — {context}");
            Assert.LessOrEqual(rect.yMax, bounds.max.y + 0.01f, $"view top edge inside region — {context}");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  1. THE GESTURE — real simulated two-finger touch
    // ═════════════════════════════════════════════════════════════════════════════
    public class Phase23PinchZoomTests : InputTestFixture
    {
        RegionManager _regions;
        Dictionary<string, Grid> _grids;
        GameState _state;
        FarmingSystem _farming;
        PlayerController _player;
        Camera _cam;
        CameraFollow _follow;
        Touchscreen _touch;

        // Both fingers on one horizontal line, so separation is pure |x1 - x2| and the
        // expected zoom ratio is readable straight off the numbers in each test.
        const float TouchY = 640f;
        const float StartSeparation = 200f;   // comfortably over MinStartSeparationPx (60)

        public override void Setup()
        {
            base.Setup();
            PinchZoom.ResetState();
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _regions.SetCurrentRegion(Phase23Rig.Vale);
            _state = new GameState(new SaveSnapshot());
            _state.Save.currentRegion = Phase23Rig.Vale;
            _farming = new FarmingSystem(_state);
            FarmGrid.Create(Phase23Rig.Vale, _grids[Phase23Rig.Vale], _farming, _state);

            _regions.TryGetBounds(Phase23Rig.Vale, out var b);
            _player = PlayerController.Spawn(b.center);
            (_cam, _follow, _) = Phase23Rig.MakeCamera(_player.transform, CameraFollow.DefaultOrthographicSize);
            _touch = InputSystem.AddDevice<Touchscreen>();
        }

        public override void TearDown()
        {
            PinchZoom.ResetState();
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            if (_player != null) Object.DestroyImmediate(_player.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
            base.TearDown();
        }

        /// <summary>Put two fingers down <paramref name="separation"/> px apart and let
        /// PinchZoom recognise the gesture (recognition happens on its own frame — the
        /// baseline separation must be sampled before any zoom is applied).</summary>
        IEnumerator BeginPinch(float separation)
        {
            BeginTouch(1, new Vector2(360f - separation / 2f, TouchY), screen: _touch);
            BeginTouch(2, new Vector2(360f + separation / 2f, TouchY), screen: _touch);
            yield return null;
            Assert.IsTrue(PinchZoom.IsPinching,
                $"two fingers {separation}px apart begin a pinch gesture");
        }

        /// <summary>Move both fingers to a new separation, symmetrically.</summary>
        IEnumerator PinchTo(float separation)
        {
            MoveTouch(1, new Vector2(360f - separation / 2f, TouchY), screen: _touch);
            MoveTouch(2, new Vector2(360f + separation / 2f, TouchY), screen: _touch);
            yield return null;
        }

        IEnumerator EndPinch(float separation)
        {
            EndTouch(1, new Vector2(360f - separation / 2f, TouchY), screen: _touch);
            EndTouch(2, new Vector2(360f + separation / 2f, TouchY), screen: _touch);
            yield return null;
        }

        // ── 1a. the convention, and that it is genuinely continuous ──────────────
        [UnityTest]
        public IEnumerator RealTwoFingerPinch_ZoomsInAndOut_StandardMobileConvention()
        {
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize, _cam.orthographicSize, 0.001f,
                "starts at the P3-Phase 20 default framing");

            yield return BeginPinch(StartSeparation);
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize, _cam.orthographicSize, 0.001f,
                "merely putting two fingers down changes nothing — the gesture only samples its baseline");

            // Fingers APART -> closer view (smaller orthographic size). Zoom is a ratio
            // against the starting separation, so 2x the spread is half the size.
            yield return PinchTo(StartSeparation * 2f);
            float zoomedIn = _cam.orthographicSize;
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize / 2f, zoomedIn, 0.01f,
                "spreading the fingers to 2x separation halves the orthographic size");
            Assert.Less(zoomedIn, CameraFollow.DefaultOrthographicSize, "spreading fingers zooms IN");

            // Intermediate step: the gesture is continuous, not a two-position toggle.
            yield return PinchTo(StartSeparation * 1.25f);
            float partway = _cam.orthographicSize;
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize / 1.25f, partway, 0.01f,
                "an intermediate separation gives an intermediate zoom");
            Assert.Greater(partway, zoomedIn, "easing the fingers back together zooms OUT again");

            // Anchored on the START separation, not frame-to-frame: returning the fingers
            // to where they began must return the exact starting zoom, with no drift from
            // the round trip out and back.
            yield return PinchTo(StartSeparation);
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize, _cam.orthographicSize, 0.01f,
                "returning the fingers to the starting separation returns the starting zoom exactly");

            yield return EndPinch(StartSeparation);
            Assert.IsFalse(PinchZoom.IsPinching, "lifting both fingers ends the gesture");
            Debug.Log($"[P23] real pinch: ortho {CameraFollow.DefaultOrthographicSize} -> {zoomedIn:F2} " +
                      $"-> {partway:F2} -> {_cam.orthographicSize:F2} via simulated Touchscreen input");
        }

        // ── 1b. bounds, driven from the gesture (not from SetOrthographicSize) ───
        [UnityTest]
        public IEnumerator RealPinch_ClampsAtMinAndMax_NeverPastTheDerivedFraming()
        {
            yield return BeginPinch(StartSeparation);

            // Spread far past the min. Unclamped this would be 14 * (200/2000) = 1.4.
            yield return PinchTo(StartSeparation * 10f);
            Assert.AreEqual(CameraFollow.MinOrthographicSize, _cam.orthographicSize, 0.001f,
                "an extreme spread clamps at MinOrthographicSize, it does not overshoot");

            // Squeeze far past the max. Unclamped this would be 14 * (200/20) = 140 —
            // way outside the region, and far below the 44px sprite legibility floor.
            yield return PinchTo(StartSeparation / 10f);
            Assert.AreEqual(CameraFollow.MaxOrthographicSize, _cam.orthographicSize, 0.001f,
                "an extreme squeeze clamps at MaxOrthographicSize");
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize, CameraFollow.MaxOrthographicSize, 0.001f,
                "max zoom-out IS the P3-Phase 20 derived framing — pinching out can return to it, never exceed it");

            // And the clamp is not sticky: the gesture still tracks fingers afterwards.
            yield return PinchTo(StartSeparation * 2f);
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize / 2f, _cam.orthographicSize, 0.01f,
                "zoom resumes tracking the fingers after touching a clamp bound");

            // The legibility budget the max is defending, restated as a number.
            float pxPerUnit = Phase23Rig.PxPerUnit(CameraFollow.MaxOrthographicSize);
            Assert.GreaterOrEqual(pxPerUnit, 44f,
                $"at max zoom-out a 1-unit sprite is {pxPerUnit:F1}px — the 44px floor still holds");
            Debug.Log($"[P23] clamp range [{CameraFollow.MinOrthographicSize}, {CameraFollow.MaxOrthographicSize}] " +
                      $"= {CameraFollow.MaxOrthographicSize / CameraFollow.MinOrthographicSize:F1}x zoom; " +
                      $"sprite renders {Phase23Rig.PxPerUnit(CameraFollow.MinOrthographicSize):F0}px in -> {pxPerUnit:F1}px out");

            yield return EndPinch(StartSeparation * 2f);
        }

        // ── 1c. a pinch must not double as a tap ────────────────────────────────
        [UnityTest]
        public IEnumerator PinchDoesNotAlsoWalkThePlayer()
        {
            var startPos = _player.transform.position;

            // The first finger of any pinch lands alone, and PlayerController's tap handler
            // has already acted on it before the second finger arrives. That is the whole
            // reason PinchZoom cancels a fresh tap-move on BeginGesture.
            BeginTouch(1, new Vector2(360f - StartSeparation / 2f, TouchY), screen: _touch);
            yield return null;
            Debug.Log($"[P23] after finger 1 alone: MoveTarget={_player.MoveTarget}");

            BeginTouch(2, new Vector2(360f + StartSeparation / 2f, TouchY), screen: _touch);
            yield return null;
            Assert.IsTrue(PinchZoom.IsPinching, "second finger begins the pinch");
            Assert.IsNull(_player.MoveTarget,
                "the stray tap from the pinch's first finger is cancelled — pinching does not walk the player");

            // Moving the fingers during the gesture must not be read as taps either.
            yield return PinchTo(StartSeparation * 2f);
            yield return PinchTo(StartSeparation * 1.5f);
            Assert.IsNull(_player.MoveTarget, "finger movement during a pinch issues no move orders");

            // Lifting one finger leaves the other pressed. That must not register as a
            // fresh press edge and fire a phantom tap where the remaining finger rests.
            EndTouch(1, new Vector2(360f - StartSeparation * 0.75f, TouchY), screen: _touch);
            yield return null;
            yield return null;
            Assert.IsNull(_player.MoveTarget,
                "lifting one finger does not fire a phantom tap from the finger still down");

            EndTouch(2, new Vector2(360f + StartSeparation * 0.75f, TouchY), screen: _touch);
            yield return null;
            Assert.IsFalse(PinchZoom.IsPinching, "gesture over");

            _player.MoveStep(0.5f);

            // Not exactly zero, and that is the honest result rather than a tolerance
            // papered over a bug: the pinch's first finger is a real press, so
            // PlayerController.Update runs ONE MoveStep on it before the second finger
            // arrives and the gesture cancels the order. The drift is bounded by a single
            // frame of moveSpeed (4.5 u/s) — 0.075 units at 60fps, well under a tenth of a
            // 1-unit cell, and it stops dead rather than continuing to the tapped point.
            float drift = Vector3.Distance(_player.transform.position, startPos);
            Assert.Less(drift, 0.1f,
                $"the player drifted {drift:F3} units during the pinch — under one frame of movement, " +
                "so the stray tap was cancelled rather than walked out");
            Debug.Log($"[P23] pinch drift: {drift:F3} units (one frame of moveSpeed {_player.moveSpeed} " +
                      "before the second finger cancelled the stray tap)");

            // ...and normal tap-to-move still works immediately afterwards, so the
            // suppression is scoped to the gesture and not left latched on.
            Vector3 screenPt = _cam.WorldToScreenPoint(startPos + new Vector3(3f, 3f, 0f));
            BeginTouch(1, new Vector2(screenPt.x, screenPt.y), screen: _touch);
            yield return null;
            EndTouch(1, new Vector2(screenPt.x, screenPt.y), screen: _touch);
            yield return null;
            Assert.IsNotNull(_player.MoveTarget, "a single tap after the pinch still moves the player");
            Debug.Log($"[P23] post-pinch single tap -> move target {_player.MoveTarget}");
        }

        // ── 1d. the cancel is scoped to the tap the pinch itself caused ─────────
        [UnityTest]
        public IEnumerator PinchCancelsOnlyTheTapItCaused_NotAnAgedWalkOrder()
        {
            // FIRST, the reality that shapes this — it failed as originally written and the
            // failure was correct. When a pinch's first finger lands on open ground it is a
            // genuine tap, so PlayerController REPLACES any in-flight walk order with it
            // before the second finger ever arrives. "My walk to the NPC survives a pinch"
            // is therefore NOT true and cannot be made true here: the old order is already
            // gone by the time PinchZoom sees a gesture at all. Pinned so nobody reads
            // TapCancelWindow as a stronger promise than it is.
            var destination = _player.transform.position + new Vector3(6f, 6f, 0f);
            _player.SetMoveTarget(destination);
            var oldOrder = _player.MoveTarget;

            BeginTouch(1, new Vector2(360f - StartSeparation / 2f, TouchY), screen: _touch);
            yield return null;
            Assert.AreNotEqual(oldOrder, _player.MoveTarget,
                "documented reality: a pinch's first finger is a real tap and replaces the previous walk order");
            EndTouch(1, new Vector2(360f - StartSeparation / 2f, TouchY), screen: _touch);
            yield return null;
            _player.CancelMove();

            // What TapCancelWindow actually protects: a gesture whose first finger issued NO
            // tap — it landed on a HUD button, so PlayerController's
            // EventSystem.IsPointerOverGameObject guard swallowed it — must leave an aged
            // walk order alone. Driven through the component's real BeginGesture (the same
            // call the touch path makes), so this exercises PlayerController's
            // CancelRecentTapMove rather than a stand-in for it.
            _player.SetMoveTarget(destination);
            Assert.IsNotNull(_player.MoveTarget);

            float deadline = Time.time + PinchZoom.TapCancelWindow + 0.2f;
            while (Time.time < deadline) yield return null;   // age the order past the window

            var pinch = _cam.GetComponent<PinchZoom>();
            pinch.BeginGestureForTest(StartSeparation);
            Assert.IsTrue(PinchZoom.IsPinching);
            Assert.IsNotNull(_player.MoveTarget,
                $"a walk order older than {PinchZoom.TapCancelWindow}s is NOT cancelled by a pinch that did not cause it");

            pinch.ApplyPinch(StartSeparation * 2f);
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize / 2f, _cam.orthographicSize, 0.01f,
                "the gesture still zooms normally");
            Assert.IsNotNull(_player.MoveTarget, "and the order survives the rest of the gesture");
            PinchZoom.ResetState();

            for (int i = 0; i < 400 && _player.MoveTarget != null; i++) _player.MoveStep(0.05f);
            Assert.Less(Vector3.Distance(_player.transform.position, destination), 0.1f,
                "the player still reaches the destination it was walking to before the pinch");
            Debug.Log($"[P23] aged walk order (> {PinchZoom.TapCancelWindow}s) survived a pinch it did not cause, " +
                      "and completed afterwards");
        }

        // ── 1e. UI immunity, measured under a real gesture ──────────────────────
        [UnityTest]
        public IEnumerator HudIsUnaffectedByRealPinchZoom()
        {
            var engine = _regions.gameObject.AddComponent<QuestEngine>();
            engine.Bind(_state);
            DialogueUI.Close();   // q_arrival's cutscene auto-opens on Bind
            var hud = HudUI.Create(_state, engine, _regions);
            var canvas = UIRoot.Canvas;
            yield return null;

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
                "UIRoot is Overlay — the camera is not in its render path at all");
            Assert.IsNull(canvas.worldCamera, "an Overlay canvas has no camera to be zoomed");

            // Sample every Text on the CANVAS: font size (reference px) AND the on-SCREEN
            // corners of its rect. For an Overlay canvas, GetWorldCorners returns screen
            // pixels, so this is the real rendered geometry, not the setup restated.
            //
            // Sampled from the canvas rather than from `hud`: HudUI.Build parents its top
            // and bottom bars to UIRoot.Canvas directly, so the HudUI GameObject has no
            // Text children at all. Walking the canvas is also the stronger check — it
            // covers the quest log, map, ranch and pause panels too, not just the HUD.
            var texts = canvas.GetComponentsInChildren<Text>(true);
            Assert.Greater(texts.Length, 0, "there is UI text to measure");
            float scaleBefore = canvas.scaleFactor;
            var sizesBefore = texts.Select(t => t.fontSize).ToArray();
            var cornersBefore = texts.Select(t =>
            {
                var c = new Vector3[4];
                ((RectTransform)t.transform).GetWorldCorners(c);
                return c;
            }).ToArray();

            try
            {
                yield return BeginPinch(StartSeparation);
                yield return PinchTo(StartSeparation * 10f);   // clamps to min
                Assert.AreEqual(CameraFollow.MinOrthographicSize, _cam.orthographicSize, 0.001f);
                yield return null;

                AssertHudUnchanged(canvas, texts, scaleBefore, sizesBefore, cornersBefore, "at min zoom");

                yield return PinchTo(StartSeparation / 10f);   // clamps to max
                Assert.AreEqual(CameraFollow.MaxOrthographicSize, _cam.orthographicSize, 0.001f);
                yield return null;

                AssertHudUnchanged(canvas, texts, scaleBefore, sizesBefore, cornersBefore, "at max zoom");
                yield return EndPinch(StartSeparation / 10f);

                Debug.Log($"[P23] UI invariant across a real {CameraFollow.MaxOrthographicSize / CameraFollow.MinOrthographicSize:F1}x " +
                          $"pinch: {texts.Length} Text elements on the canvas, scaleFactor {canvas.scaleFactor:F3} " +
                          "unchanged, every rect identical to the screen pixel");
            }
            finally
            {
                Object.DestroyImmediate(hud.QuestLog.gameObject);
                Object.DestroyImmediate(hud.Map.gameObject);
                Object.DestroyImmediate(hud.Pause.gameObject);
                Object.DestroyImmediate(hud.Ranch.gameObject);
                Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(engine);
            }
        }

        static void AssertHudUnchanged(Canvas canvas, Text[] texts, float scaleBefore,
                                       int[] sizesBefore, Vector3[][] cornersBefore, string context)
        {
            Assert.AreEqual(scaleBefore, canvas.scaleFactor, 0.0001f,
                $"CanvasScaler scale factor is zoom-independent — {context}");
            for (int i = 0; i < texts.Length; i++)
            {
                Assert.AreEqual(sizesBefore[i], texts[i].fontSize,
                    $"'{texts[i].name}' font size unchanged — {context}");
                var now = new Vector3[4];
                ((RectTransform)texts[i].transform).GetWorldCorners(now);
                for (int c = 0; c < 4; c++)
                    Assert.AreEqual(cornersBefore[i][c], now[c],
                        $"'{texts[i].name}' screen corner {c} unchanged — {context}");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  2. THE CLAMP — legal box must be recomputed per zoom level
    // ═════════════════════════════════════════════════════════════════════════════
    public class Phase23ZoomClampTests
    {
        RegionManager _regions;
        Dictionary<string, Grid> _grids;
        PlayerController _player;
        Camera _cam;
        CameraFollow _follow;
        Bounds _bounds;

        /// <summary>Every zoom level the clamp has to be correct at: both bounds, the
        /// default, and awkward in-between values a real gesture actually passes through.</summary>
        static readonly float[] ZoomLevels =
        {
            CameraFollow.MinOrthographicSize, 6.4f, 8f, 11.3f, CameraFollow.DefaultOrthographicSize,
        };

        [SetUp]
        public void SetUp()
        {
            PinchZoom.ResetState();
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _regions.SetCurrentRegion(Phase23Rig.Vale);
            _regions.TryGetBounds(Phase23Rig.Vale, out _bounds);
            _player = PlayerController.Spawn(_bounds.center);
            (_cam, _follow, _) = Phase23Rig.MakeCamera(_player.transform, CameraFollow.DefaultOrthographicSize);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            if (_player != null) Object.DestroyImmediate(_player.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            GameEvents.Clear();
        }

        /// <summary>Corners and beyond — the camera is clamped, the player is clamped by
        /// RegionManager, and both must hold at every zoom.</summary>
        Vector3[] ExtremeFoci() => new[]
        {
            _bounds.min, _bounds.max,
            new Vector3(_bounds.min.x, _bounds.max.y), new Vector3(_bounds.max.x, _bounds.min.y),
            _bounds.center + new Vector3(0f, 100f), _bounds.center + new Vector3(-100f, 0f),
            _bounds.center,
        };

        // ── 2a. the box is a FUNCTION of zoom, not a constant ───────────────────
        [Test]
        public void ClampedFocus_RecomputesTheLegalBox_PerZoomLevel()
        {
            // The failure this pins: a clamp derived once for ortho 14 and reused. If that
            // happened, clamping the same out-of-bounds focus at different zooms would give
            // the SAME answer. It must not — the inset is orthographicSize (and
            // orthographicSize * aspect), so every level has a strictly different box.
            var farCorner = _bounds.max + new Vector3(50f, 50f, 0f);
            var results = new List<(float zoom, Vector3 clamped)>();

            foreach (float z in ZoomLevels)
            {
                _follow.SetOrthographicSize(z);
                var clamped = _follow.ClampedFocus(farCorner);
                results.Add((z, clamped));

                // Analytically: clamped corner == region max minus the current half-extents.
                float halfH = z, halfW = z * Phase23Rig.Aspect;
                Assert.AreEqual(_bounds.max.x - halfW, clamped.x, 0.001f,
                    $"x inset equals the CURRENT halfW at ortho {z}");
                Assert.AreEqual(_bounds.max.y - halfH, clamped.y, 0.001f,
                    $"y inset equals the CURRENT halfH at ortho {z}");
            }

            for (int i = 1; i < results.Count; i++)
            {
                Assert.Less(results[i].clamped.x, results[i - 1].clamped.x - 0.001f,
                    $"zooming out from {results[i - 1].zoom} to {results[i].zoom} pulls the clamp further in on x");
                Assert.Less(results[i].clamped.y, results[i - 1].clamped.y - 0.001f,
                    $"zooming out from {results[i - 1].zoom} to {results[i].zoom} pulls the clamp further in on y");
            }

            float minPan = RegionWorldBuilder.Size - CameraFollow.MinOrthographicSize * Phase23Rig.Aspect * 2f;
            float maxPan = RegionWorldBuilder.Size - CameraFollow.MaxOrthographicSize * Phase23Rig.Aspect * 2f;
            Assert.Greater(minPan, maxPan,
                "the pan box is genuinely larger when zoomed in — the clamp is live, not frozen at ortho 14");
            Debug.Log($"[P23] horizontal pan range: {minPan:F1} units at ortho {CameraFollow.MinOrthographicSize} " +
                      $"-> {maxPan:F1} units at ortho {CameraFollow.MaxOrthographicSize} (region is {RegionWorldBuilder.Size})");
        }

        // ── 2b. BOTH callers use the recomputed value ───────────────────────────
        [UnityTest]
        public IEnumerator LateUpdateAndSnapTo_BothUseTheCurrentZoomsClamp()
        {
            // P3-Phase 20's bug was SnapTo bypassing ClampedFocus entirely. The zoom-shaped
            // version is SnapTo clamping against a stale size. Assert the two paths agree
            // EXACTLY at every zoom level, from the same extreme focus.
            foreach (float z in ZoomLevels)
            {
                _follow.SetOrthographicSize(z);
                Assert.AreEqual(z, _cam.orthographicSize, 0.001f);

                foreach (var focus in ExtremeFoci())
                {
                    var expected = _follow.ClampedFocus(focus);

                    // Path A: SnapTo.
                    _follow.SnapTo(focus);
                    Assert.AreEqual(expected.x, _cam.transform.position.x, 0.001f,
                        $"SnapTo lands on the ortho-{z} clamp for focus {focus}");
                    Assert.AreEqual(expected.y, _cam.transform.position.y, 0.001f,
                        $"SnapTo lands on the ortho-{z} clamp for focus {focus}");
                    Phase23Rig.AssertViewInsideRegion(_cam, _bounds, $"SnapTo at ortho {z}, focus {focus}");

                    // Path B: LateUpdate, driven by really moving the player there.
                    _player.SetMoveTarget(focus);
                    for (int i = 0; i < 6000 && _player.MoveTarget != null; i++) _player.MoveStep(0.05f);
                    yield return null;   // one real LateUpdate on the live component
                    Phase23Rig.AssertViewInsideRegion(_cam, _bounds, $"LateUpdate at ortho {z}, focus {focus}");
                    Assert.IsTrue(Phase23Rig.ViewRect(_cam).Contains(
                        new Vector2(_player.transform.position.x, _player.transform.position.y)) ||
                        Vector2.Distance(_player.transform.position, _cam.transform.position) < z * 2f,
                        $"player stays framed at ortho {z} near {focus}");
                }
            }
            Debug.Log($"[P23] SnapTo and LateUpdate agree with ClampedFocus at all " +
                      $"{ZoomLevels.Length} zoom levels x {ExtremeFoci().Length} extreme foci");
        }

        // ── 2c. zooming OUT must re-clamp immediately, not over 0.18s of glide ──
        [UnityTest]
        public IEnumerator ZoomingOut_ReclampsTheCameraImmediately_NotOverTheSmoothDamp()
        {
            // Zoom in, pan hard into a corner — legal at ortho 5, well outside the legal
            // box at ortho 14. Zooming back out must correct the position on the SAME
            // frame; leaving it to SmoothDamp would show outside the region for ~0.18s,
            // which is the exact failure P3-Phase 20 fixed in SnapTo by another route.
            _follow.SetOrthographicSize(CameraFollow.MinOrthographicSize);
            _player.SetMoveTarget(_bounds.max);
            for (int i = 0; i < 6000 && _player.MoveTarget != null; i++) _player.MoveStep(0.05f);
            _follow.SnapTo(_player.transform.position);
            yield return null;

            var cornerAtMinZoom = _cam.transform.position;
            Phase23Rig.AssertViewInsideRegion(_cam, _bounds, "panned to the corner at min zoom");

            float insetAtMax = CameraFollow.MaxOrthographicSize;
            Assert.Greater(cornerAtMinZoom.y, _bounds.max.y - insetAtMax,
                "precondition: this camera position is ILLEGAL at max zoom, so the re-clamp has real work to do");

            _follow.SetOrthographicSize(CameraFollow.MaxOrthographicSize);
            // No yield — assert on the very same frame the zoom changed.
            Phase23Rig.AssertViewInsideRegion(_cam, _bounds,
                "immediately after zooming out, with no frame of SmoothDamp in between");

            yield return null;
            Phase23Rig.AssertViewInsideRegion(_cam, _bounds, "and still inside on the following frame");
            Debug.Log($"[P23] zoom-out re-clamp: camera {cornerAtMinZoom} (legal at ortho " +
                      $"{CameraFollow.MinOrthographicSize}) -> {_cam.transform.position} on the same frame " +
                      $"at ortho {CameraFollow.MaxOrthographicSize}");
        }

        // ── 2d. the clamp holds through a continuous sweep, not just at endpoints ─
        [UnityTest]
        public IEnumerator ContinuousZoomSweep_NeverShowsOutsideTheRegion()
        {
            // A real pinch passes through every value between the bounds. Walk the whole
            // range in small steps while parked in a corner, checking every one.
            _player.SetMoveTarget(new Vector3(_bounds.min.x, _bounds.max.y, 0f));
            for (int i = 0; i < 6000 && _player.MoveTarget != null; i++) _player.MoveStep(0.05f);
            yield return null;

            const int Steps = 40;
            for (int i = 0; i <= Steps; i++)
            {
                float z = Mathf.Lerp(CameraFollow.MinOrthographicSize, CameraFollow.MaxOrthographicSize, i / (float)Steps);
                _follow.SetOrthographicSize(z);
                Phase23Rig.AssertViewInsideRegion(_cam, _bounds, $"zoom sweep out, ortho {z:F2}");
                yield return null;
                Phase23Rig.AssertViewInsideRegion(_cam, _bounds, $"zoom sweep out (after LateUpdate), ortho {z:F2}");
            }
            for (int i = Steps; i >= 0; i--)
            {
                float z = Mathf.Lerp(CameraFollow.MinOrthographicSize, CameraFollow.MaxOrthographicSize, i / (float)Steps);
                _follow.SetOrthographicSize(z);
                Phase23Rig.AssertViewInsideRegion(_cam, _bounds, $"zoom sweep in, ortho {z:F2}");
            }
            Debug.Log($"[P23] {Steps * 2 + 2} zoom steps across [{CameraFollow.MinOrthographicSize}, " +
                      $"{CameraFollow.MaxOrthographicSize}] parked in a region corner: view never left the region");
        }

        // ── 2e. the bounds themselves are sane against the world geometry ───────
        [Test]
        public void ZoomBounds_AreConsistentWithTheDerivedFramingAndTheRegionSize()
        {
            Assert.AreEqual(CameraFollow.DefaultOrthographicSize, CameraFollow.MaxOrthographicSize, 0.001f,
                "max zoom-out is exactly the P3-Phase 20 derived framing (landmark ring + 44px floor)");
            Assert.Less(CameraFollow.MinOrthographicSize, CameraFollow.MaxOrthographicSize,
                "the zoom range is non-empty");

            // At max the camera must still be able to follow at all (Phase 20's guard).
            Assert.Less(CameraFollow.MaxOrthographicSize * 2f, RegionWorldBuilder.Size,
                "at max zoom the camera still pans rather than pinning to region centre");

            // At min the widest tool AoE (3x3 — till t3, water t3, harvest t2) must fit in
            // the narrow portrait dimension with room to spare, or the closest view crops
            // the very thing it exists to let you aim.
            float visibleWidth = CameraFollow.MinOrthographicSize * Phase23Rig.Aspect * 2f;
            Assert.Greater(visibleWidth, 3f + 2f,
                $"at min zoom the view is {visibleWidth:F1} units wide — a 3x3 tool AoE plus margin fits");

            // Min zoom is an integer upscale of the source art under Point filtering.
            float scaleVsSource = Phase23Rig.PxPerUnit(CameraFollow.MinOrthographicSize) / Phase23Rig.SpriteSourcePx;
            Assert.AreEqual(Mathf.Round(scaleVsSource), scaleVsSource, 0.001f,
                $"min zoom renders 64px art at {scaleVsSource:F2}x — a non-integer factor duplicates source " +
                "rows unevenly under Point filtering (see MinZoomCandidateSweep_Screenshots)");
            Debug.Log($"[P23] zoom bounds [{CameraFollow.MinOrthographicSize}, {CameraFollow.MaxOrthographicSize}]: " +
                      $"min shows {visibleWidth:F1}x{CameraFollow.MinOrthographicSize * 2f:F1} units at " +
                      $"{Phase23Rig.PxPerUnit(CameraFollow.MinOrthographicSize):F0} px/unit ({scaleVsSource:F2}x source art)");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════════
    //  3. THE EVIDENCE — render split (calls Camera.Render)
    // ═════════════════════════════════════════════════════════════════════════════
    public class Phase23ZoomCaptureTests
    {
        const int W = Phase23Rig.W, H = Phase23Rig.H;

        RegionManager _regions;
        Dictionary<string, Grid> _grids;
        GameState _state;
        FarmingSystem _farming;
        PlayerController _player;
        Camera _cam;
        CameraFollow _follow;

        [SetUp]
        public void SetUp()
        {
            PinchZoom.ResetState();
            GameEvents.Clear();
            GameDatabase.LoadAll();
            _regions = new GameObject("TestRegions").AddComponent<RegionManager>();
            _grids = RegionWorldBuilder.BuildAll(_regions);
            _regions.SetCurrentRegion(Phase23Rig.Vale);
            _state = new GameState(new SaveSnapshot());
            _state.Save.currentRegion = Phase23Rig.Vale;
            _farming = new FarmingSystem(_state);
            FarmGrid.Create(Phase23Rig.Vale, _grids[Phase23Rig.Vale], _farming, _state);
        }

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) Object.DestroyImmediate(_cam.gameObject);
            if (_player != null) Object.DestroyImmediate(_player.gameObject);
            foreach (var npc in Object.FindObjectsByType<NpcController>())
                Object.DestroyImmediate(npc.gameObject);
            foreach (var kv in _grids) Object.DestroyImmediate(kv.Value.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
            var ui = GameObject.Find("UIRoot");
            if (ui != null) Object.DestroyImmediate(ui);
            UIRoot.Reset();
            GameEvents.Clear();
        }

        /// <summary>Mature crops around the spawn point, so the captures show what a player
        /// actually looks at rather than bare tilemap (borrowed from Phase20CameraFramingTests).</summary>
        void PlantAroundCentre()
        {
            _regions.TryGetBounds(Phase23Rig.Vale, out var b);
            var origin = b.center - new Vector3(RegionWorldBuilder.Size / 2f, RegionWorldBuilder.Size / 2f);
            var crop = GameDatabase.Crops.FirstOrDefault(c => c.region == Phase23Rig.Vale);
            if (crop == null) return;
            int cx = Mathf.RoundToInt(b.center.x - origin.x), cy = Mathf.RoundToInt(b.center.y - origin.y);
            for (int dx = -4; dx <= 4; dx += 2)
                for (int dy = -4; dy <= 4; dy += 2)
                {
                    int x = cx + dx, y = cy + dy;
                    if (!_farming.Till(x, y)) continue;
                    _state.Save.seeds[crop.id] = 1;
                    if (!_farming.Plant(x, y, crop.id)) continue;
                    var p = _farming.PlotAt(x, y);
                    p.crop.stage = crop.stages - 1;
                    p.crop.needsWater = false;
                }
        }

        void Capture(string fileName)
        {
            var rt = new RenderTexture(W, H, 24);
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", fileName), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>Canvas capture: the RenderTexture must be BOUND ACROSS A FRAME, because a
        /// ScreenSpaceCamera canvas sizes itself to the camera's pixelRect and would otherwise
        /// lay out against the tiny batchmode Screen (P3-Phase 22's finding, reused verbatim).</summary>
        IEnumerator CaptureWithCanvas(string fileName)
        {
            var rt = new RenderTexture(W, H, 24);
            _cam.targetTexture = rt;
            _cam.aspect = Phase23Rig.Aspect;
            yield return null;
            yield return null;

            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", "Logs", fileName), tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ── 3a. the sweep MinOrthographicSize is derived from ───────────────────
        [UnityTest]
        public IEnumerator MinZoomCandidateSweep_Screenshots()
        {
            // Same method P3-Phase 20 used to pick the default: capture the candidates and
            // measure what is actually visible, rather than picking a number blind. Re-runnable,
            // so a future re-tune is a screenshot comparison instead of a fresh guess.
            float[] candidates = { 4f, 5f, 6f, 7f, CameraFollow.MaxOrthographicSize };

            PlantAroundCentre();
            NpcSpawner.SpawnAll(_regions, _state);
            _regions.TryGetBounds(Phase23Rig.Vale, out var b);
            _player = PlayerController.Spawn(b.center);
            yield return null;
            yield return null;   // let FarmPlot/NPC visuals refresh from live state

            (_cam, _follow, _) = Phase23Rig.MakeCamera(_player.transform, candidates[0]);

            foreach (float size in candidates)
            {
                // Bypass SetOrthographicSize deliberately: the sweep must be able to show
                // candidates BELOW the shipped minimum, which is the point of a sweep.
                _cam.orthographicSize = size;
                _cam.aspect = Phase23Rig.Aspect;
                yield return null;
                Capture($"phase23_minzoom_candidate_ortho{size:00}.png");

                float pxPerUnit = Phase23Rig.PxPerUnit(size);
                float scale = pxPerUnit / Phase23Rig.SpriteSourcePx;
                bool integerScale = Mathf.Abs(scale - Mathf.Round(scale)) < 0.001f;
                Debug.Log($"[P23Sweep] ortho {size,2}: view {size * Phase23Rig.Aspect * 2f:F1} x {size * 2f:F1} units " +
                          $"({size * Phase23Rig.Aspect * 2f / RegionWorldBuilder.Size * 100f:F0}% x " +
                          $"{size * 2f / RegionWorldBuilder.Size * 100f:F0}% of the region), " +
                          $"{pxPerUnit:F1} px/unit = {scale:F2}x the 64px source art " +
                          $"({(integerScale ? "INTEGER upscale — clean pixel doubling" : "non-integer — Point filtering duplicates rows unevenly")}), " +
                          $"widest 3x3 tool AoE occupies {3f / (size * Phase23Rig.Aspect * 2f) * 100f:F0}% of the frame width");
            }

            Debug.Log($"[P23Sweep] shipped MinOrthographicSize = {CameraFollow.MinOrthographicSize} " +
                      $"(see CameraFollow for the derivation this sweep feeds)");
        }

        // ── 3b. the shipped bounds, with the HUD in frame ───────────────────────
        [UnityTest]
        public IEnumerator PinchZoom_MinAndMax_Screenshots()
        {
            PlantAroundCentre();
            NpcSpawner.SpawnAll(_regions, _state);
            _regions.TryGetBounds(Phase23Rig.Vale, out var b);
            _player = PlayerController.Spawn(b.center);
            _state.AddCoins(5000);

            var engine = _regions.gameObject.AddComponent<QuestEngine>();
            engine.Bind(_state);
            DialogueUI.Close();
            var hud = HudUI.Create(_state, engine, _regions);
            var prompt = FarmActionPrompt.Create(_player.transform);

            (_cam, _follow, _) = Phase23Rig.MakeCamera(_player.transform, CameraFollow.MaxOrthographicSize);

            // Overlay canvases don't render into a RenderTexture; flip to ScreenSpaceCamera
            // for the capture only. In the shipping build UIRoot stays Overlay.
            var canvas = UIRoot.Canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;

            try
            {
                DialogueUI.Close();
                yield return null;

                _follow.SetOrthographicSize(CameraFollow.MaxOrthographicSize);
                yield return CaptureWithCanvas("phase23_zoom_max_ortho14.png");
                Debug.Log($"[P23] captured max zoom-out at ortho {_cam.orthographicSize}: " +
                          $"{Phase23Rig.PxPerUnit(_cam.orthographicSize):F1} px per world unit, " +
                          $"canvas scaleFactor {canvas.scaleFactor:F3}");

                _follow.SetOrthographicSize(CameraFollow.MinOrthographicSize);
                yield return CaptureWithCanvas("phase23_zoom_min_ortho05.png");
                Debug.Log($"[P23] captured min zoom-in at ortho {_cam.orthographicSize}: " +
                          $"{Phase23Rig.PxPerUnit(_cam.orthographicSize):F1} px per world unit, " +
                          $"canvas scaleFactor {canvas.scaleFactor:F3} (UNCHANGED — the HUD is the same " +
                          "size in both captures while the world art is " +
                          $"{CameraFollow.MaxOrthographicSize / CameraFollow.MinOrthographicSize:F1}x bigger)");
            }
            finally
            {
                Object.DestroyImmediate(prompt.gameObject);
                Object.DestroyImmediate(hud.QuestLog.gameObject);
                Object.DestroyImmediate(hud.Map.gameObject);
                Object.DestroyImmediate(hud.Pause.gameObject);
                Object.DestroyImmediate(hud.Ranch.gameObject);
                Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(engine);
            }
        }
    }
}
