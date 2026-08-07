// Cosmic Bloom — P3-Phase 21 legibility: quest-giver NPCs looked exactly like
// the 40-odd NPCs who have nothing to hand out, so the only way to find the next
// quest was to walk the entire roster tapping everyone.
//
// This shows a pulsing "!" above an NPC whose quest can actually be started by
// talking to them, and a dimmer one while you're carrying a quest of theirs.
// Availability is NOT guessed from npcs.json's quest links or from appearance:
// it comes from QuestEngine.StateForGiver, which shares its predicate with the
// activation loop in OnTalkedTo. If the icon is up, chatting starts a quest.
//
// Reuses InteractionCue for the pulse rather than growing a second animation
// component; the glyph and colors are what distinguish it.
using CosmicBloom.Core;
using UnityEngine;

namespace CosmicBloom.World
{
    [RequireComponent(typeof(NpcController))]
    public class QuestGiverIndicator : MonoBehaviour
    {
        /// <summary>Gold: talking to them starts a quest right now.</summary>
        static readonly Color AvailableColor = new Color(1f, 0.82f, 0.22f, 1f);
        /// <summary>Muted cream: you already carry one of their quests — they're worth
        /// revisiting, but there's nothing new to pick up.</summary>
        static readonly Color InProgressColor = new Color(0.72f, 0.78f, 0.88f, 1f);

        const float PollSeconds = 0.5f;   // quest state changes on events, not per frame

        NpcController _npc;
        QuestEngine _engine;
        InteractionCue _cue;
        float _timer;

        /// <summary>Test/inspection access — what the icon is currently advertising.</summary>
        public QuestEngine.GiverState Current { get; private set; } = QuestEngine.GiverState.None;

        /// <summary>Test/inspection access — is the icon actually on screen?</summary>
        public bool IsShowing => _cue != null && _cue.gameObject.activeSelf;

        public static QuestGiverIndicator Attach(GameObject npc)
        {
            var ind = npc.AddComponent<QuestGiverIndicator>();
            // Above the NPC's head and clear of the existing "tap me" InteractionCue
            // (yOffset 0.9) so the two never overlap into one unreadable blob. Scale 0.36
            // (up from a first pass at 0.22): at 0.22 the glyph came out ~9px wide in a
            // real 720px capture — present, but not something you'd notice across a room.
            ind._cue = InteractionCue.Attach(npc, AvailableColor, yOffset: 1.25f, scale: 0.36f,
                                             sprite: ProceduralSprites.Bang(), name: "QuestGiverIcon");
            ind._cue.SetVisible(false);
            return ind;
        }

        void Awake() => _npc = GetComponent<NpcController>();

        void Start() => Refresh();

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < PollSeconds) return;
            _timer = 0f;
            Refresh();
        }

        /// <summary>Re-read quest state and show/hide/retint. Public so tests can drive it
        /// deterministically instead of waiting out the poll interval.</summary>
        public void Refresh()
        {
            if (_npc == null) _npc = GetComponent<NpcController>();
            if (_engine == null) _engine = FindAnyObjectByType<QuestEngine>();
            if (_npc?.Def == null || _engine == null || _engine.State == null)
            {
                SetState(QuestEngine.GiverState.None);
                return;
            }
            SetState(_engine.StateForGiver(_npc.Def.id));
        }

        void SetState(QuestEngine.GiverState state)
        {
            Current = state;
            if (_cue == null) return;

            // SetActive (not renderer.enabled) on purpose: NpcController.ApplyTimeOfDay
            // re-enables every child Renderer when the NPC is visible for its activeAt
            // window, which would switch a hidden icon back on. An inactive GameObject
            // survives that, and a sleeping NPC still correctly hides its icon with the
            // rest of its hierarchy.
            _cue.SetVisible(state != QuestEngine.GiverState.None);
            if (state == QuestEngine.GiverState.Available) _cue.SetColor(AvailableColor);
            else if (state == QuestEngine.GiverState.InProgress) _cue.SetColor(InProgressColor);
        }
    }
}
