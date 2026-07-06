// Cosmic Bloom — loader for the procedurally generated crop/NPC pixel art
// (Tools/art/build-cosmic.mjs output, imported into
// Assets/Resources/Sprites/Generated/). Thin Resources.Load wrapper with a
// cache; returns null (never throws) when a sprite is missing so callers can
// fall back to the existing ProceduralSprites tinted-shape placeholders.
using System.Collections.Generic;
using UnityEngine;

namespace CosmicBloom.World
{
    public static class GeneratedSprites
    {
        const string CropsRoot = "Sprites/Generated/Crops/";
        const string NpcsRoot = "Sprites/Generated/Npcs/";
        const string AnimalsRoot = "Sprites/Generated/Animals/";
        const string TilesRoot = "Sprites/Generated/Tiles/";

        static readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>Generated sprite for a crop at a given game-stage index, or null if not generated.</summary>
        public static Sprite Crop(string cropId, int stage) => Load($"{CropsRoot}{cropId}_{stage}");

        /// <summary>Generated body sprite for an NPC, or null if not generated.</summary>
        public static Sprite Npc(string npcId) => Load($"{NpcsRoot}{npcId}");

        /// <summary>Generated body sprite for an animal, or null if not generated.</summary>
        public static Sprite Animal(string animalId) => Load($"{AnimalsRoot}{animalId}");

        /// <summary>Generated ground-tier texture mask for a region (grayscale — tinted by
        /// Tile.color, not pre-colored), or null if not generated.</summary>
        public static Sprite Tile(string regionId, int tier) => Load($"{TilesRoot}{regionId}_tier{tier}");

        static Sprite Load(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>(resourcePath);
            _cache[resourcePath] = sprite;   // cache misses (null) too — avoids repeat Resources.Load calls
            return sprite;
        }

        /// <summary>Test hygiene: clears the cache (e.g. between PlayMode test cases that reload Resources state).</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
