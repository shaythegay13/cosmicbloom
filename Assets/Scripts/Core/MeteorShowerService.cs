// Cosmic Bloom — Phase 2 minimal scheduled event (q_meteor_mystery's wait_for
// step; q_comet_chaser's witness_event counts the same firings). A full procedural
// event system is out of scope (GDD lists it under "future"); this just needs
// the event to occur once, observably. Reuses TimeManager.OnNightfall — a
// meteor shower streaks over every night, firing GameEvents.MeteorShower.
using CosmicBloom.UI;
using UnityEngine;

namespace CosmicBloom.Core
{
    public class MeteorShowerService : MonoBehaviour
    {
        public static MeteorShowerService Instance { get; private set; }

        public static MeteorShowerService Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("MeteorShowerService");
            return go.AddComponent<MeteorShowerService>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Subscribe();
        }

        void OnEnable() => Subscribe();

        bool _subscribed;
        void Subscribe()
        {
            if (_subscribed || TimeManager.Instance == null) return;
            TimeManager.Instance.OnNightfall += Fire;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed && TimeManager.Instance != null)
                TimeManager.Instance.OnNightfall -= Fire;
            if (Instance == this) Instance = null;
        }

        void Fire()
        {
            if (!DialogueUI.IsOpen)
                DialogueUI.Show("The Sky", "A meteor shower streaks overhead!");
            GameEvents.MeteorShower();
            Debug.Log("[MeteorShower] fired at nightfall");
        }
    }
}
