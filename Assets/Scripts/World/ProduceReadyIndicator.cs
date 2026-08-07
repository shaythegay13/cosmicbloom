// Cosmic Bloom — P3-Phase 21 legibility: an animal whose produce was ready
// signalled it only by multiplying its body tint by 1.25. On the generated art
// that tint starts at pure white, so the "cue" clamped to no visible change at
// all — livestock ran their whole Phase 4b/8 species clock silently and the only
// way to find ready produce was to tap every animal in the region.
//
// This shows a pulsing sparkle above any animal that currently has produce
// waiting. It reads AnimalController.ProduceReady directly, so it tracks the
// per-SPECIES clock exactly: collecting from one instance reschedules the
// species and every other instance's icon drops on the next poll, matching the
// Phase 8 one-produce-stream rule rather than promising five.
//
// Companions with produce in data (star_slime, comet_duck) get the icon too —
// the Phase 18 role split is behavioral, not economic, and their produce is
// collected by the same code path.
using UnityEngine;

namespace CosmicBloom.World
{
    [RequireComponent(typeof(AnimalController))]
    public class ProduceReadyIndicator : MonoBehaviour
    {
        /// <summary>Warm amber — deliberately paired with the sparkle glyph so it never
        /// reads as the quest-giver "!" (which is a similar gold but a bar+dot). Pushed
        /// oranger than the first pass's pale gold because the first live capture put it
        /// over a white chicken, where a near-white gold vanished into the sprite.</summary>
        static readonly Color ReadyColor = new Color(1f, 0.72f, 0.12f, 1f);

        const float PollSeconds = 0.5f;

        AnimalController _animal;
        InteractionCue _cue;
        float _timer;

        /// <summary>Test/inspection access — is the icon actually on screen?</summary>
        public bool IsShowing => _cue != null && _cue.gameObject.activeSelf;

        public static ProduceReadyIndicator Attach(GameObject animal)
        {
            var ind = animal.AddComponent<ProduceReadyIndicator>();
            // yOffset 0.95 clears the animal sprite entirely — at 0.78 the sparkle sat
            // inside the chicken's silhouette in the first live capture.
            ind._cue = InteractionCue.Attach(animal, ReadyColor, yOffset: 0.95f, scale: 0.38f,
                                             sprite: ProceduralSprites.Sparkle(), name: "ProduceReadyIcon");
            ind._cue.SetVisible(false);
            return ind;
        }

        void Awake() => _animal = GetComponent<AnimalController>();

        void Start() => Refresh();

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < PollSeconds) return;
            _timer = 0f;
            Refresh();
        }

        /// <summary>Re-read the species produce clock and show/hide. Public so tests can
        /// drive it deterministically instead of waiting out the poll interval.</summary>
        public void Refresh()
        {
            if (_animal == null) _animal = GetComponent<AnimalController>();
            bool ready = _animal != null && _animal.Def != null && _animal.ProduceReady;
            if (_cue != null) _cue.SetVisible(ready);
        }
    }
}
