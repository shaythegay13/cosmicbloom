// Cosmic Bloom — one controller for all 50 NPCs. Everything specific to an
// NPC (name, region, palette, day/night presence, dialogue, quest links)
// comes from npcs.json via GameDatabase — no per-NPC subclasses.
using CosmicBloom.Core;
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public class NpcController : MonoBehaviour, IInteractable
    {
        public NpcDef Def { get; private set; }

        SpriteRenderer _body;
        bool _subscribed;
        GameState _state;
        static readonly System.Collections.Generic.HashSet<string> _metFallback = new();

        public Vector3 InteractPosition => transform.position;

        /// <summary>Test hygiene for the no-save met tracker.</summary>
        public static void ResetMetFallback() => _metFallback.Clear();

        /// <summary>Spawn NPC #index of its region's roster, spread around the region ring.</summary>
        public static NpcController Spawn(NpcDef def, Vector3 regionCenter, int index, int regionCount,
                                          GameState state = null)
        {
            var go = new GameObject($"Npc_{def.id}");

            // deterministic ring placement: mid-tier radius, evenly spaced
            float angle = (Mathf.PI * 2f * index) / Mathf.Max(1, regionCount) + 0.4f;
            const float radius = 7.2f;
            go.transform.position = regionCenter +
                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            var body = go.AddComponent<SpriteRenderer>();
            body.sprite = ProceduralSprites.Capsule();
            body.color = ProceduralSprites.ParseHex(def.palette != null && def.palette.TryGetValue("body", out var b) ? b : null);
            body.sortingOrder = 9;
            var mat = Resources.Load<Material>("Materials/FarmPlotSprite");
            if (mat != null) body.sharedMaterial = mat;

            // accent chip so same-body NPCs still differ visually
            var accentGo = new GameObject("Accent");
            accentGo.transform.SetParent(go.transform, false);
            accentGo.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            accentGo.transform.localScale = Vector3.one * 0.28f;
            var accent = accentGo.AddComponent<SpriteRenderer>();
            accent.sprite = ProceduralSprites.Square(32, 4);
            accent.color = ProceduralSprites.ParseHex(def.palette != null && def.palette.TryGetValue("accent", out var a) ? a : null);
            accent.sortingOrder = 10;
            if (mat != null) accent.sharedMaterial = mat;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.55f;

            var npc = go.AddComponent<NpcController>();
            npc.Def = def;
            npc._body = body;
            npc._state = state;
            npc.ApplyTimeOfDay();
            return npc;
        }

        void OnEnable() => Subscribe();
        void Start() => Subscribe();

        void Subscribe()
        {
            if (_subscribed || TimeManager.Instance == null) return;
            TimeManager.Instance.OnSunrise += ApplyTimeOfDay;
            TimeManager.Instance.OnNightfall += ApplyTimeOfDay;
            _subscribed = true;
        }

        void OnDestroy()
        {
            if (_subscribed && TimeManager.Instance != null)
            {
                TimeManager.Instance.OnSunrise -= ApplyTimeOfDay;
                TimeManager.Instance.OnNightfall -= ApplyTimeOfDay;
            }
        }

        /// <summary>Day-only NPCs sleep at night and vice versa (activeAt from data).</summary>
        public void ApplyTimeOfDay()
        {
            bool visible = Def.activeAt switch
            {
                "day" => TimeManager.Instance == null || TimeManager.Instance.IsDay,
                "night" => TimeManager.Instance != null && TimeManager.Instance.IsNight,
                _ => true,
            };
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = visible;
        }

        public void Interact(PlayerController player)
        {
            bool met = _state != null ? _state.Save.npcsMet.Contains(Def.id) : _metFallback.Contains(Def.id);

            string line = Def.dialogue != null && Def.dialogue.TryGetValue(met ? "idle" : "intro", out var d)
                ? d
                : $"{Def.name} nods at you.";

            if (!met)
            {
                if (_state != null) { _state.Save.npcsMet.Add(Def.id); _state.MarkDirty(); }
                else _metFallback.Add(Def.id);
            }

            UI.DialogueUI.Show(Def.name, line, () => GameEvents.TalkedTo(Def.id));
        }
    }
}
