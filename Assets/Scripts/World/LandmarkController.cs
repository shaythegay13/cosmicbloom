// Cosmic Bloom — restorable landmarks from regions.json (broken greenhouse,
// moonlit pond, …). One controller for all 17; identity comes from data.
// Tap when in range: pay restoreCost coins → restored (persisted in
// FarmState.landmarksRestored) → GameEvents.LandmarkRestored feeds
// restore_landmark quest steps.
//
// Phase 2: once restored, a landmark's own `unlocks` string (already in data)
// doubles as a behavior key — "fishing" turns the pond into a fish-catching
// tap point, "lore_journal_2" (the arboretum) accepts seed deposits. No new
// per-landmark ids added; both checks key off the field regions.json already has.
using CosmicBloom.Core;
using CosmicBloom.Data;
using System.Linq;
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
                if (Def.unlocks == "fishing") { TryFish(); return; }
                if (Def.unlocks == "lore_journal_2" && TryDepositSeeds()) return;
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

        /// <summary>Fishing action at a restored "fishing" landmark (the moonlit pond).
        /// Requires an owned fish-action tool (fishing_rod, quest-granted at 0 cost).</summary>
        void TryFish()
        {
            bool hasRod = GameDatabase.Tools.Any(t => t.action == "fish" && _state.Save.tools.Contains(t.id));
            if (!hasRod)
            {
                UI.DialogueUI.Show(Def.name, "The water ripples invitingly, but you have no fishing rod.");
                return;
            }
            const int coins = 8;   // small per-catch value (no fish price data)
            _state.AddCoins(coins);
            GameEvents.FishCaught();
            Debug.Log($"[Landmark] fish caught at {Def.id} (+{coins}c)");
            UI.DialogueUI.Show(Def.name, $"You catch a shimmering fish! (+{coins} coins)");
        }

        /// <summary>Deposit-seeds action at a restored "lore_journal_2" landmark (the
        /// arboretum). Delegates the inventory check/consumption to the quest engine,
        /// which owns Save.seeds. Returns false (falls through to flavor text) when
        /// there's no active deposit_seeds step or the pouch doesn't qualify yet.</summary>
        bool TryDepositSeeds()
        {
            var quests = FindFirstObjectByType<QuestEngine>();
            if (quests == null) return false;
            if (!quests.HasActiveStepOfType("deposit_seeds")) return false;

            if (quests.TryDepositSeeds())
            {
                UI.DialogueUI.Show(Def.name, "You stock the seed vault with seeds from your pouch.");
                return true;
            }
            UI.DialogueUI.Show(Def.name, "The seed vault needs more variety — bring seeds from at least 3 different crops.");
            return true;
        }
    }
}
