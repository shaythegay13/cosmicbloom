// Cosmic Bloom — tap-to-move + tap-to-interact, new Input System only
// (project has activeInputHandler=1; legacy Input throws here).
// Tap empty ground: walk there (clamped to current region bounds).
// Tap an IInteractable: interact if in range, else walk over and interact on arrival.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicBloom.World
{
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 4.5f;
        public float interactRange = 1.4f;

        /// <summary>Where the player is headed, or null when idle. Exposed for tests/UI.</summary>
        public Vector3? MoveTarget { get; private set; }

        IInteractable _pendingInteract;
        Camera _cam;
        bool _pressWasHeld;

        void Awake() => _cam = Camera.main;

        void Update()
        {
            HandleTap();
            MoveStep(Time.deltaTime);
        }

        void HandleTap()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            // own edge detection: wasPressedThisFrame misses presses when editor
            // and dynamic input updates interleave (PlayMode tests, some editors)
            bool held = pointer.press.isPressed;
            bool pressedNow = held && !_pressWasHeld;
            _pressWasHeld = held;
            if (!pressedNow) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

            Vector2 screen = pointer.position.ReadValue();
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            OnTapWorld(world);
        }

        /// <summary>Route a world-space tap: interact or move. Public for tests.</summary>
        public void OnTapWorld(Vector3 world)
        {
            world.z = 0f;
            var hit = Physics2D.OverlapPoint(world);
            var interactable = hit != null ? hit.GetComponentInParent<IInteractable>() : null;

            if (interactable != null)
            {
                if (InRange(interactable))
                {
                    CancelMove();
                    interactable.Interact(this);
                    return;
                }
                _pendingInteract = interactable;
                SetMoveTarget(interactable.InteractPosition);
                return;
            }

            _pendingInteract = null;
            SetMoveTarget(world);
        }

        public void SetMoveTarget(Vector3 world)
        {
            world.z = 0f;
            MoveTarget = RegionManager.Instance != null
                ? RegionManager.Instance.ClampToCurrentRegion(world)
                : world;
        }

        public void CancelMove()
        {
            MoveTarget = null;
            _pendingInteract = null;
        }

        /// <summary>Advance movement by dt. Split out so tests can drive it deterministically.</summary>
        public void MoveStep(float dt)
        {
            if (MoveTarget == null) return;

            transform.position = Vector3.MoveTowards(transform.position, MoveTarget.Value, moveSpeed * dt);

            if ((transform.position - MoveTarget.Value).sqrMagnitude < 0.0004f)
            {
                transform.position = MoveTarget.Value;
                MoveTarget = null;

                var pending = _pendingInteract;
                _pendingInteract = null;
                if (pending != null && InRange(pending))
                    pending.Interact(this);
            }
        }

        bool InRange(IInteractable target) =>
            Vector2.Distance(transform.position, target.InteractPosition) <= interactRange;

        /// <summary>Spawn the player with its placeholder sprite. Used by GameBootstrap and tests.</summary>
        public static PlayerController Spawn(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Capsule();
            sr.color = new Color32(0x7d, 0xd8, 0xc8, 0xff);   // teal cosmic-gardener placeholder
            sr.sortingOrder = 10;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;   // 2D-lit so day/night lighting applies
            return go.AddComponent<PlayerController>();
        }
    }
}
