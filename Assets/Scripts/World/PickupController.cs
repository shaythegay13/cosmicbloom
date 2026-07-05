// Cosmic Bloom — Phase 2: world pickups for "collect" quest steps whose item
// isn't animal produce (clan_journal, rare_seed, prism_shard, chart_fragment).
// Tap to collect: fires GameEvents.ItemGranted, which the quest engine's
// existing collect-step Bump already consumes. Despawn is handled by
// PickupSpawner's reconcile once the step's progress no longer needs this item.
using CosmicBloom.Core;
using UnityEngine;

namespace CosmicBloom.World
{
    public class PickupController : MonoBehaviour, IInteractable
    {
        public string ItemId { get; private set; }

        public Vector3 InteractPosition => transform.position;

        public static PickupController Spawn(string itemId, Vector3 position)
        {
            var go = new GameObject($"Pickup_{itemId}");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.6f;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square(24, 3);
            sr.color = new Color(0.95f, 0.85f, 0.35f, 1f);   // glinting-find gold
            sr.sortingOrder = 7;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.6f;

            var pu = go.AddComponent<PickupController>();
            pu.ItemId = itemId;
            return pu;
        }

        public void Interact(PlayerController player)
        {
            UI.DialogueUI.Show("Found Something", $"You found: {ItemId.Replace('_', ' ')}.",
                () => GameEvents.ItemGranted(ItemId));
        }
    }
}
