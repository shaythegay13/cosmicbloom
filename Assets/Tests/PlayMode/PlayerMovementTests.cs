// Phase 1 verification — tap-to-move: player converges on targets, clamps to
// region bounds, and walks-then-interacts with IInteractable targets.
// World is built in-test (no scene dependency) with the same components
// GameBootstrap wires at runtime.
using System.Collections;
using CosmicBloom.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicBloom.Tests
{
    public class PlayerMovementTests
    {
        RegionManager _regions;
        PlayerController _player;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestRegionManager");
            _regions = go.AddComponent<RegionManager>();
            // Same shape as StarlightVale: 20x20 world units starting at origin
            _regions.RegisterRegion("starlight_vale", new Bounds(new Vector3(10, 10), new Vector3(20, 20)));
            _player = PlayerController.Spawn(new Vector3(10, 10));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_regions.gameObject);
        }

        [UnityTest]
        public IEnumerator TapMovesPlayerToTarget()
        {
            var start = _player.transform.position;
            _player.OnTapWorld(new Vector3(14, 12));

            Assert.IsNotNull(_player.MoveTarget, "tap should set a move target");
            Debug.Log($"[P1Test] start={start} target={_player.MoveTarget}");

            float timeout = Time.time + 5f;
            while (_player.MoveTarget != null && Time.time < timeout) yield return null;

            Debug.Log($"[P1Test] arrived={_player.transform.position}");
            Assert.IsNull(_player.MoveTarget, "player should arrive within 5s");
            Assert.Less(Vector3.Distance(_player.transform.position, new Vector3(14, 12)), 0.05f);
        }

        [UnityTest]
        public IEnumerator TapOutsideBoundsClampsToRegionEdge()
        {
            _player.OnTapWorld(new Vector3(500, -300));   // far outside the 20x20 region

            Assert.IsNotNull(_player.MoveTarget);
            var t = _player.MoveTarget.Value;
            Debug.Log($"[P1Test] clamped target={t}");
            Assert.LessOrEqual(t.x, 20f, "x clamped to region max");
            Assert.GreaterOrEqual(t.y, 0f, "y clamped to region min");

            float timeout = Time.time + 8f;
            while (_player.MoveTarget != null && Time.time < timeout) yield return null;

            var p = _player.transform.position;
            Debug.Log($"[P1Test] stopped at={p}");
            Assert.IsTrue(p.x >= 0f && p.x <= 20f && p.y >= 0f && p.y <= 20f,
                $"player must stay inside region bounds, was {p}");
        }

        class FakeInteractable : MonoBehaviour, IInteractable
        {
            public int interactCount;
            public Vector3 InteractPosition => transform.position;
            public void Interact(PlayerController player) => interactCount++;
        }

        [UnityTest]
        public IEnumerator TapInteractableWalksOverThenInteracts()
        {
            var target = new GameObject("Interactable");
            target.transform.position = new Vector3(15, 10);
            target.AddComponent<BoxCollider2D>().size = Vector2.one;
            var fake = target.AddComponent<FakeInteractable>();
            yield return new WaitForFixedUpdate();   // let the collider register

            _player.OnTapWorld(new Vector3(15, 10));
            Assert.AreEqual(0, fake.interactCount, "too far away — should walk first");
            Assert.IsNotNull(_player.MoveTarget);

            float timeout = Time.time + 5f;
            while (fake.interactCount == 0 && Time.time < timeout) yield return null;

            Debug.Log($"[P1Test] interactCount={fake.interactCount} playerPos={_player.transform.position}");
            Assert.AreEqual(1, fake.interactCount, "interaction should fire on arrival");

            Object.DestroyImmediate(target);
        }

        [UnityTest]
        public IEnumerator TapInteractableInRangeInteractsImmediately()
        {
            var target = new GameObject("Interactable");
            target.transform.position = new Vector3(10.8f, 10);   // within 1.4 interactRange
            target.AddComponent<BoxCollider2D>().size = Vector2.one;
            var fake = target.AddComponent<FakeInteractable>();
            yield return new WaitForFixedUpdate();

            _player.OnTapWorld(new Vector3(10.8f, 10));
            Assert.AreEqual(1, fake.interactCount, "in range — immediate interact");
            Assert.IsNull(_player.MoveTarget, "no walking needed");

            Object.DestroyImmediate(target);
        }
    }
}
