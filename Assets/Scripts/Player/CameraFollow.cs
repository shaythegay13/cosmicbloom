// Cosmic Bloom — smooth camera follow, kept inside the current region bounds
// when the region is larger than the view; centers on small regions.
using UnityEngine;

namespace CosmicBloom.World
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smoothTime = 0.18f;

        Camera _cam;
        Vector3 _velocity;

        void Awake() => _cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);

            if (_cam != null && _cam.orthographic && RegionManager.Instance != null)
            {
                var b = RegionManager.Instance.CurrentBounds;
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                desired.x = b.size.x <= halfW * 2f
                    ? b.center.x
                    : Mathf.Clamp(desired.x, b.min.x + halfW, b.max.x - halfW);
                desired.y = b.size.y <= halfH * 2f
                    ? b.center.y
                    : Mathf.Clamp(desired.y, b.min.y + halfH, b.max.y - halfH);
            }

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        /// <summary>Jump instantly (region travel), skipping the smooth glide across the world.</summary>
        public void SnapTo(Vector3 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            _velocity = Vector3.zero;
        }
    }
}
