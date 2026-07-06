// Import settings for procedurally generated crop/NPC pixel art
// (Tools/art/build-cosmic.mjs output, written to
// Assets/Resources/Sprites/Generated/). Runs automatically on import via
// AssetPostprocessor.OnPreprocessTexture — no manual per-sprite setup.
//
// PPU reasoning (also logged in BUILD_LOG.md Phase 5): the shared generator
// (Tools/art/style.mjs) draws every sprite on a TILE=32 logical-pixel canvas
// at SCALE=2, so every exported PNG is exactly 64x64. Farm tiles are exactly
// 1 world unit (FarmGrid's Grid.cellSize defaults to (1,1,1), unchanged by
// RegionWorldBuilder), so PPU=64 makes one generated sprite occupy exactly
// one farm tile — matching the crop case exactly (a planted crop should fill
// its plot) and putting NPCs in the same ballpark as the old placeholder
// capsule (26x34 px @ PPU 32 = 0.81x1.06 world units) without a second,
// harder-to-justify magic number.
using UnityEditor;
using UnityEngine;

public class GeneratedSpriteImporter : AssetPostprocessor
{
    const string GeneratedRoot = "Assets/Resources/Sprites/Generated/";
    const string NpcsRoot = "Assets/Resources/Sprites/Generated/Npcs/";
    const float Ppu = 64f;

    void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains(GeneratedRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = Ppu;

        // Crops: center pivot, same as the placeholder Square sprite they
        // replace (FarmPlot centers the plot GameObject on the grid cell).
        // NPCs: bottom-center pivot — the character art's feet sit right at
        // the bottom edge of the 32px canvas (chibi standing pose), so a
        // bottom pivot grounds them at NpcController's spawn point the same
        // way the old near-bottom-pivoted Capsule (0.5, 0.15) did.
        bool isNpc = path.Contains(NpcsRoot);
        var pivot = isNpc ? new Vector2(0.5f, 0.06f) : new Vector2(0.5f, 0.5f);
        importer.spritePivot = pivot;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)(isNpc ? SpriteAlignment.Custom : SpriteAlignment.Center);
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
    }
}
