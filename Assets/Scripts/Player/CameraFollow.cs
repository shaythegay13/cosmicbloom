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

        public Transform target;
        public float smoothTime = 0.18f;

        Camera _cam;
        Vector3 _velocity;

        void Awake() => _cam = GetComponent<Camera>();

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
        /// following at all (Phase20CameraFramingTests guards that edge).</summary>
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
