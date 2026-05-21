using UnityEditor;
using UnityEngine;

namespace NumiDream.Editor
{
    internal sealed class NumiDreamArtImportSettings : AssetPostprocessor
    {
        private const string SharedArtRoot = "Assets/NumiDream/Art/";
        private const string StageOneArtRoot = "Assets/NumiDream/StageOne/Art/";
        private const string SpriteSheetRoot = "Assets/NumiDream/Art/Characters/Nomi/SpriteSheets/";
        private const string StageOneCollectiblesRoot = "Assets/NumiDream/StageOne/Art/Collectibles/";
        private const float DefaultSpritePixelsPerUnit = 100f;
        private const float CrumblingHandPixelsPerUnit = DefaultSpritePixelsPerUnit;
        private const string CrumblingHandFramesRoot = "Assets/NumiDream/StageOne/Art/Puzzles/Crumbling-Hand/Frames/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SharedArtRoot) && !assetPath.StartsWith(StageOneArtRoot))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            var isKnownSpriteSheet = TryGetSpriteSheetLayout(assetPath, out _);
            var isCrumblingHandFrame = assetPath.StartsWith(CrumblingHandFramesRoot);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = isKnownSpriteSheet
                ? SpriteImportMode.Multiple
                : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = isCrumblingHandFrame
                ? CrumblingHandPixelsPerUnit
                : DefaultSpritePixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = isKnownSpriteSheet || isCrumblingHandFrame
                ? TextureImporterCompression.Uncompressed
                : TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = isCrumblingHandFrame ? 100 : importer.compressionQuality;
            importer.maxTextureSize = 8192;

            if (isCrumblingHandFrame)
            {
                ApplyHighQualityStandaloneSettings(importer);
            }
        }

        private static void ApplyHighQualityStandaloneSettings(TextureImporter importer)
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed,
                compressionQuality = 100,
                crunchedCompression = false,
                resizeAlgorithm = TextureResizeAlgorithm.Mitchell
            });
        }

        private static bool TryGetSpriteSheetLayout(string path, out SpriteSheetLayout layout)
        {
            switch (path)
            {
                case SpriteSheetRoot + "Nomi-Idle-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout(828, 1910, 6, 6);
                    return true;
                case SpriteSheetRoot + "Nomi-Walk-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout(916, 2495, 5, 5);
                    return true;
                case SpriteSheetRoot + "Nomi-Jump-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout(1068, 2203, 5, 9);
                    return true;
                case StageOneCollectiblesRoot + "Memory-Fragment/Memory-Fragment-Sprite-Sheet.png":
                    layout = new SpriteSheetLayout(1200, 1388, 5, 20);
                    return true;
                default:
                    layout = default;
                    return false;
            }
        }

        private readonly struct SpriteSheetLayout
        {
            public SpriteSheetLayout(int frameWidth, int frameHeight, int columns, int frameCount)
            {
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                Columns = columns;
                FrameCount = frameCount;
            }

            public int FrameWidth { get; }
            public int FrameHeight { get; }
            public int Columns { get; }
            public int FrameCount { get; }
        }
    }
}
