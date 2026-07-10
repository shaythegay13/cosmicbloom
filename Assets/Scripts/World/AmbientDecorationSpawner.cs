// Cosmic Bloom — scatters small non-interactive decoration (rocks, grass
// tufts) across each region's wild/transitional ground (tiers 0/1), so those
// rings don't read as flat empty color next to the farmable core. Purely
// visual: no collider, no save state, deterministic per region so the layout
// doesn't reshuffle between sessions.
using CosmicBloom.Data;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class AmbientDecorationSpawner
    {
        const float SpawnChance = 0.14f;
        const int SortingOrder = 1;   // above the ground tilemap (0), below plots/NPCs/landmarks

        public static void SpawnForRegion(RegionDef def, Vector3 origin, int regionIndex, Transform parent)
        {
            var rng = new System.Random(regionIndex * 7919 + 17);   // fixed per region, independent of any global RNG state
            var mat = ProceduralSprites.LitMaterial;

            var root = new GameObject("Decorations");
            root.transform.SetParent(parent, false);

            Color tint = ProceduralSprites.ParseHex(
                def.groundTiers != null && def.groundTiers.Count > 0 ? def.groundTiers[0] : null);
            tint = Color.Lerp(tint, Color.black, 0.35f);   // richer/darker than the ground itself, reads as a distinct prop

            for (int y = 0; y < RegionWorldBuilder.Size; y++)
            {
                for (int x = 0; x < RegionWorldBuilder.Size; x++)
                {
                    if (RegionWorldBuilder.IsFarmable(x, y)) continue;   // never on the tillable core
                    if (rng.NextDouble() >= SpawnChance) continue;

                    bool tuft = rng.NextDouble() < 0.5;
                    var go = new GameObject(tuft ? "Tuft" : "Rock");
                    go.transform.SetParent(root.transform, false);
                    go.transform.position = origin + new Vector3(
                        x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.5f,
                        y + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.5f, 0f);
                    float scale = 0.7f + (float)rng.NextDouble() * 0.5f;
                    go.transform.localScale = Vector3.one * scale;

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = tuft ? ProceduralSprites.Tuft() : ProceduralSprites.Rock();
                    sr.color = tint;
                    sr.sortingOrder = SortingOrder;
                    if (mat != null) sr.sharedMaterial = mat;
                }
            }
        }
    }
}
