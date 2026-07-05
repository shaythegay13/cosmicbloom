// Cosmic Bloom — anything the player can tap to interact with (plots, NPCs, animals).
// Tapping an interactable out of range walks the player over, then interacts on arrival.
using UnityEngine;

namespace CosmicBloom.World
{
    public interface IInteractable
    {
        /// <summary>Where the player should stand to interact.</summary>
        Vector3 InteractPosition { get; }
        void Interact(PlayerController player);
    }
}
