// Cosmic Bloom — smooth camera follow, kept inside the current region bounds
// when the region is larger than the view; centers on small regions.
using UnityEngine;

namespace CosmicBloom.World
{
    public class CameraFollow : MonoBehaviour
    {
        /// <summary>Default framing for a 40x40 region (P3-Phase 15 geometry).
        /// Lived as a bare `7f` literal in GameBootstrap from Phase 2's 20x20 era until
        /// P3-Phase 20 — Phase 15 quadrupled region area and updated only the comment,
        /// so the view kept showing ~7% of a region. Named here, not in GameBootstrap,
        /// so the clamp math below and the framing tests read the same single value.
        ///
        /// 14 is derived, not taste: it is the smallest half-height that reaches the
        /// landmark ring, whose furthest slot sits at +14 on Y (LandmarkSpawner.Slots),
        /// and it also brings the NPC home ring (r=14.4, NpcController) to the frame
        /// edge — so standing on the spawn point shows a landmark and an NPC instead of
        /// bare tilemap. Scale it with those two if the region ever resizes again.
        /// The cost is readability: art is 64px at PPU 64, so a 1-unit sprite renders at
        /// 45.7px on a 1280px-tall portrait frame — above the 44px floor, but Point
        /// filtering now downscales source art (0.71x). Don't raise this much further
        /// without an art pass; see Phase20CameraFramingTests for the measured budget.</summary>
        public const float DefaultOrthographicSize = 14f;

        /// <summary>Furthest the player may pinch out (P3-Phase 23). Deliberately the
        /// SAME value as the default framing rather than a looser ceiling: 14 is the
        /// smallest half-height that reaches the landmark ring, and it is already
        /// 0.71x downscaled against the 64px source art (45.7 screen px per world unit
        /// on a 1280px portrait frame, against a 44px floor). Anything beyond it buys
        /// no new landmark and drops sprites under that floor, so zoom-out is a way to
        /// RETURN to the derived framing, never to exceed it.</summary>
        public const float MaxOrthographicSize = DefaultOrthographicSize;

        /// <summary>Closest the player may pinch in (P3-Phase 23). Chosen by the same
        /// sweep-and-measure method as the default: candidates 4/5/6/7 were captured at
        /// 720x1280 portrait (Phase23PinchZoomTests.MinZoomCandidateSweep_Screenshots)
        /// and measured, not eyeballed.
        ///
        ///   ortho  px/unit  vs 64px source  visible world units (portrait)
        ///     4     160.0      2.50x          4.5 x 8.0
        ///     5     128.0      2.00x          5.6 x 10.0
        ///     6     106.7      1.67x          6.8 x 12.0
        ///     7      91.4      1.43x          7.9 x 14.0
        ///
        /// 5 is the pick: it is the only candidate that lands on an EXACT 2x integer
        /// upscale of the 64px art, which under this project's Point filtering is the
        /// difference between clean doubled pixels and the uneven row duplication of
        /// 1.43x/1.67x/2.50x. It still shows 5.6 x 10 units — comfortably more than the
        /// 3-unit span of the widest tool AoE (till t3 / water t3 / harvest t2 are all
        /// 3x3) plus margin — where 4 narrows the portrait view to 4.5 units and crops
        /// that AoE against the frame edges. Zoom range is 14/5 = 2.8x.</summary>
        public const float MinOrthographicSize = 5f;

        public Transform target;
        public float smoothTime = 0.18f;

        Camera _cam;
        Vector3 _velocity;

        void Awake() => _cam = GetComponent<Camera>();

        /// <summary>Live orthographic size, read straight off the camera — this component
        /// caches no copy of it, so the clamp below and any zoom source can't disagree.</summary>
        public float OrthographicSize
        {
            get
            {
                if (_cam == null) _cam = GetComponent<Camera>();
                return _cam != null ? _cam.orthographicSize : DefaultOrthographicSize;
            }
        }

        /// <summary>The one way to change zoom. Clamps into
        /// [<see cref="MinOrthographicSize"/>, <see cref="MaxOrthographicSize"/>] and returns
        /// the value actually applied.
        ///
        /// The re-clamp on the last line is the load-bearing part. ClampedFocus derives the
        /// legal camera box from the CURRENT size, so zooming OUT shrinks that box — a
        /// position that was legal at ortho 5 near a region edge is outside the region at
        /// ortho 14. Without correcting here, the camera would sit out of bounds for the
        /// ~0.18s SmoothDamp takes to ease back in: the same "visible for a few frames"
        /// failure P3-Phase 20 fixed in SnapTo, arriving by a different route.</summary>
        public float SetOrthographicSize(float size)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return DefaultOrthographicSize;

            float clamped = Mathf.Clamp(size, MinOrthographicSize, MaxOrthographicSize);
            if (_cam.orthographicSize == clamped) return clamped;

            _cam.orthographicSize = clamped;
            transform.position = ClampedFocus(transform.position);
            return clamped;
        }

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(
                transform.position, ClampedFocus(target.position), ref _velocity, smoothTime);
        }

        /// <summary>Where the camera may sit to frame <paramref name="focus"/> without
        /// showing anything outside the current region. At orthographic size S the legal
        /// box is inset by S vertically and S*aspect horizontally, so a larger S shrinks
        /// the pan range — at S >= half the region the camera pins to centre and stops
        /// following at all (Phase20CameraFramingTests guards that edge).
        ///
        /// The inset is read off the LIVE camera every call, never cached or captured at
        /// the default size — that is what makes it correct under pinch-zoom (P3-Phase 23),
        /// where S changes continuously mid-gesture. Both callers (LateUpdate and SnapTo)
        /// and SetOrthographicSize go through this one method; there is no second copy of
        /// the bounds maths to fall out of step, which is the shape of the SnapTo bug
        /// P3-Phase 20 fixed. Phase23PinchZoomTests asserts the box actually differs
        /// between zoom levels rather than trusting that it does.</summary>
        public Vector3 ClampedFocus(Vector3 focus)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            var desired = new Vector3(focus.x, focus.y, transform.position.z);
            if (_cam == null || !_cam.orthographic || RegionManager.Instance == null) return desired;

            var b = RegionManager.Instance.CurrentBounds;
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            desired.x = b.size.x <= halfW * 2f
                ? b.center.x
                : Mathf.Clamp(desired.x, b.min.x + halfW, b.max.x - halfW);
            desired.y = b.size.y <= halfH * 2f
                ? b.center.y
                : Mathf.Clamp(desired.y, b.min.y + halfH, b.max.y - halfH);
            return desired;
        }

        /// <summary>Jump instantly (region travel), skipping the smooth glide across the
        /// world. Goes through the same clamp as LateUpdate — snapping raw to the player
        /// put the camera outside the region for the frames it took SmoothDamp to ease
        /// back in, which is visible as a lurch on arrival now that the view is wide
        /// enough (P3-Phase 20) for the clamp to actually bite near a region edge.</summary>
        public void SnapTo(Vector3 position)
        {
            transform.position = ClampedFocus(position);
            _velocity = Vector3.zero;
        }
    }
}
