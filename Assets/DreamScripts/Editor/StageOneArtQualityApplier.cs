using System.IO;
using UnityEditor;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class StageOneArtQualityApplier
    {
        private const string CrumblingHandFramesRoot = "Assets/NumiDream/StageOne/Art/Puzzles/Crumbling-Hand/Frames";
        private const string RequestPath = "Assets/DreamScripts/Editor/StageOneArtQualityApplier.request";
        static StageOneArtQualityApplier()
        {

            if (!File.Exists(RequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    ApplyQualitySettings();
                }
                finally
                {
                    DeleteRequestFiles();
                    AssetDatabase.Refresh();
                }
            };
        }

        private static void ApplyFromMenu()
        {
            ApplyQualitySettings();
        }

        private static void ApplyQualitySettings()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { CrumblingHandFramesRoot });
            var changed = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.compressionQuality = 100;

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

                importer.SaveAndReimport();
                changed++;
            }

            Debug.Log("[StageOneArtQualityApplier] Reimported " + changed + " crumbling-hand textures with high-quality settings.");
        }

        private static void DeleteRequestFiles()
        {
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }

            var metaPath = RequestPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }
    }
}
