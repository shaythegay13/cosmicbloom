// Cosmic Bloom — restorable landmarks from regions.json (broken greenhouse,
// moonlit pond, …). One controller for all 17; identity comes from data.
// Tap when in range: pay restoreCost coins → restored (persisted in
// FarmState.landmarksRestored) → GameEvents.LandmarkRestored feeds
// restore_landmark quest steps.
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public class LandmarkController : MonoBehaviour, IInteractable
    {
        public LandmarkDef Def { get; private set; }
        public string RegionId { get; private set; }

        GameState _state;
        SpriteRenderer _sr;

        static readonly Color RuinedColor = new Color(0.35f, 0.32f, 0.30f, 1f);

        public bool Restored =>
            _state != null && _state.FarmFor(RegionId).landmarksRestored.Contains(Def.id);

        public Vector3 InteractPosition => transform.position;

        public static LandmarkController Spawn(LandmarkDef def, string regionId, Vector3 position, GameState state)
        {
            var go = new GameObject($"Landmark_{def.id}");
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Square(32, 2);
            sr.sortingOrder = 4;
            var mat = ProceduralSprites.LitMaterial;
            if (mat != null) sr.sharedMaterial = mat;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            var lm = go.AddComponent<LandmarkController>();
            lm.Def = def;
            lm.RegionId = regionId;
            lm._state = state;
            lm._sr = sr;
            lm.RefreshVisual();
            return lm;
        }

        void RefreshVisual()
        {
            if (_sr == null) return;
            _sr.color = Restored ? new Color(0.55f, 0.95f, 0.85f, 1f) : RuinedColor;
        }

        public void Interact(PlayerController player)
        {
            if (Restored)
            {
                UI.DialogueUI.Show(Def.name, $"{Def.name} hums with restored starlight. ({Def.unlocks})");
                return;
            }

            if (_state.SpendCoins(Def.restoreCost))
            {
                _state.FarmFor(RegionId).landmarksRestored.Add(Def.id);
                _state.MarkDirty();
                RefreshVisual();
                GameEvents.LandmarkRestored(Def.id, RegionId);
                Debug.Log($"[Landmark] restored {Def.id} in {RegionId} (-{Def.restoreCost}c)");
                UI.DialogueUI.Show(Def.name, $"You restore {Def.name}! Its light returns to the Vale. (-{Def.restoreCost} coins)");
            }
            else
            {
                UI.DialogueUI.Show(Def.name, $"Restoring {Def.name} needs {Def.restoreCost} coins. Keep farming!");
            }
        }
    }
}
