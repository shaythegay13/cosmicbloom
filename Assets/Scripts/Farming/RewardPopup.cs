// Cosmic Bloom — P3-Phase 17 live-verify finding E: harvest rewards only showed
// up as HUD counter changes in the corner of the screen, easy to miss. This is a
// floating "+Nc  +Nxp" label that rises and fades at the plot itself, in addition
// to (not instead of) the HUD update. World-space TextMesh, same pattern as
// FarmActionPrompt/FarmActionEffects — self-destroys after its lifetime.
using UnityEngine;

namespace CosmicBloom.Farming
{
    public class RewardPopup : MonoBehaviour
    {
        const float Lifetime = 1.1f;
        const float RiseSpeed = 0.7f;

        TextMesh _mesh;
        float _age;

        /// <summary>Fire-and-forget popup at a plot's world position. No-ops if there's
        /// nothing to show (shouldn't happen for a real harvest, but crops with a zero
        /// sellPrice/xp — none exist today — would otherwise show an empty "+0c +0xp").</summary>
        public static void Spawn(Vector3 worldPos, int coins, int xp)
        {
            if (coins <= 0 && xp <= 0) return;

            var go = new GameObject("RewardPopup");
            go.transform.position = worldPos + Vector3.up * 0.6f;

            var mesh = go.AddComponent<TextMesh>();
            mesh.characterSize = 0.13f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(1f, 0.88f, 0.4f);
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mesh.font.material;
            mr.sortingOrder = 31;

            mesh.text = coins > 0 && xp > 0 ? $"+{coins}c  +{xp}xp" : coins > 0 ? $"+{coins}c" : $"+{xp}xp";

            var popup = go.AddComponent<RewardPopup>();
            popup._mesh = mesh;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Lifetime) { Destroy(gameObject); return; }

            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            float t = _age / Lifetime;
            var c = _mesh.color;
            c.a = 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);   // hold at full alpha, then fade in the back half
            _mesh.color = c;
        }
    }
}
