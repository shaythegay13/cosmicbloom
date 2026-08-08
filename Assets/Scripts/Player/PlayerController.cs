// Cosmic Bloom — tap-to-move + tap-to-interact, new Input System only
// (project has activeInputHandler=1; legacy Input throws here).
// Tap empty ground: walk there (clamped to current region bounds).
// Tap an IInteractable: interact if in range, else walk over and interact on arrival.
using CosmicBloom.Save;
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
        Vector2Int? _pendingTill;   // free-form till target reached by walking there
        Camera _cam;
        bool _pressWasHeld;
        float _lastTapMoveTime = float.NegativeInfinity;   // see CancelRecentTapMove

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

            // A two-finger pinch (P3-Phase 23) owns the gesture. Note _pressWasHeld is
            // updated BEFORE this bail-out: lifting one finger at the end of a pinch
            // leaves the other still pressed, and a stale edge there would fire a
            // phantom tap at whatever the remaining finger happens to be resting on.
            if (PinchZoom.IsPinching) return;

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

            // Livestock place mode (P3-Phase 18) takes the tap before anything else —
            // while armed from RanchUI, the next ground tap positions the animal.
            // Placement is a management action, so it resolves where you tapped rather
            // than walking there first (unlike the free-form till below).
            if (AnimalPlacement.IsArmed && RegionManager.Instance != null)
            {
                string regionId = RegionManager.Instance.CurrentRegionId;
                if (RegionManager.Instance.TryGetBounds(regionId, out var rb))
                {
                    var c = RegionWorldBuilder.WorldToCell(rb.center, world);
                    string animalId = AnimalPlacement.Armed;
                    bool placed = AnimalSpawner.TryPlace(
                        SaveManager.Instance != null ? SaveManager.Instance.State : null,
                        RegionManager.Instance, regionId, animalId, c.x, c.y);
                    if (placed) AnimalPlacement.Disarm();
                    else Debug.Log($"[Player] cannot place {animalId} at ({c.x},{c.y})");
                    return;
                }
            }

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
                _pendingTill = null;
                SetMoveTarget(interactable.InteractPosition);
                return;
            }

            // Free-form farming (P3-Phase 17): tapping bare, tillable ground tills a
            // fresh plot there. Already-tilled cells own a FarmPlot collider and route
            // through the IInteractable branch above; only the first till comes here.
            var fg = RegionManager.Instance != null
                ? FarmGrid.ForRegion(RegionManager.Instance.CurrentRegionId) : null;
            if (fg != null && fg.TryTargetTill(world, out var tillPos, out var tillCell))
            {
                if (Vector2.Distance(transform.position, tillPos) <= interactRange)
                {
                    CancelMove();
                    fg.TillAt(tillCell);
                    return;
                }
                _pendingInteract = null;
                _pendingTill = tillCell;
                SetMoveTarget(tillPos);
                return;
            }

            _pendingInteract = null;
            _pendingTill = null;
            SetMoveTarget(world);
        }

        public void SetMoveTarget(Vector3 world)
        {
            world.z = 0f;
            MoveTarget = RegionManager.Instance != null
                ? RegionManager.Instance.ClampToCurrentRegion(world)
                : world;
            _lastTapMoveTime = Time.time;
        }

        /// <summary>Drop a move order issued within the last <paramref name="window"/>
        /// seconds. PinchZoom calls this when a gesture begins: the pinch's FIRST finger
        /// lands alone, so HandleTap has already read it as a tap and sent the player
        /// walking before the second finger arrives. The window is what keeps this from
        /// being a blunt CancelMove — a pinch must not abandon a walk-to-NPC the player
        /// ordered seconds earlier and is still waiting on.</summary>
        public void CancelRecentTapMove(float window)
        {
            if (MoveTarget == null) return;
            if (Time.time - _lastTapMoveTime > window) return;
            CancelMove();
        }

        public void CancelMove()
        {
            MoveTarget = null;
            _pendingInteract = null;
            _pendingTill = null;
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
                var pendingTill = _pendingTill;
                _pendingInteract = null;
                _pendingTill = null;
                if (pending != null && InRange(pending))
                    pending.Interact(this);
                else if (pendingTill.HasValue && RegionManager.Instance != null)
                    FarmGrid.ForRegion(RegionManager.Instance.CurrentRegionId)?.TillAt(pendingTill.Value);
            }
        }

        bool InRange(IInteractable target) =>
            Vector2.Distance(transform.position, target.InteractPosition) <= interactRange;

        /// <summary>Spawn the player. Used by GameBootstrap and tests.
        /// Same real-art/fallback pattern as NpcController: the generated
        /// character sprite if it loaded, else the tinted placeholder capsule.</summary>
        public static PlayerController Spawn(Vector3 position)
        {
            var go = new GameObject("Player");
            go.transform.position = position;

            // Sprite lives on a child "Visual" node, not the root, so MovementAnimator's
            // bob/squash-stretch (below) never perturbs transform.position — the value
            // MoveStep, camera follow, and interactRange checks all read.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var sr = visual.AddComponent<SpriteRenderer>();
            var generated = GeneratedSprites.Player();
            sr.sprite = generated != null ? generated : ProceduralSprites.Capsule();
            sr.color = generated != null
                ? Color.white                                  // art carries its own palette
                : new Color32(0x7d, 0xd8, 0xc8, 0xff);         // teal cosmic-gardener placeholder
            sr.sortingOrder = 10;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;   // 2D-lit so day/night lighting applies

            var anim = go.AddComponent<MovementAnimator>();
            anim.Init(visual.transform);

            return go.AddComponent<PlayerController>();
        }
    }
}
