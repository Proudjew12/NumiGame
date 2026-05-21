using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class ForegroundOverlaySeamFixer
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/ForegroundOverlaySeamFixer.request";
        private const string OverlayPath = "Assets/NumiDream/StageOne/Art/ParallaxLayers/021_Front_DarkVignetteOverlay.png";
        private const string SeamlessOverlayPath = "Assets/NumiDream/StageOne/Art/ParallaxLayers/021_Front_DarkVignetteOverlay_SeamlessWide.png";
        private const float HorizontalOverscan = 2.0f;
        private const float VerticalScaleMultiplier = 1.35f;
        private const float MinimumWorldWidth = 270f;
        private const float MinimumWorldHeight = 62f;

        static ForegroundOverlaySeamFixer()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    FixForegroundOverlaySeams();
                }
                finally
                {
                    DeleteRequestFiles();
                    AssetDatabase.Refresh();
                }
            };
        }

        private static void FixFromMenu()
        {
            FixForegroundOverlaySeams();
        }

        private static void FixForegroundOverlaySeams()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[ForegroundOverlaySeamFixer] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var overlayPath = File.Exists(ProjectRelativeFilePath(SeamlessOverlayPath))
                ? SeamlessOverlayPath
                : OverlayPath;

            ConfigureOverlayTextureImporter(overlayPath);
            AssetDatabase.ImportAsset(overlayPath, ImportAssetOptions.ForceUpdate);

            var frontGroundRoot = FindObject(scene, "FrontGround");
            if (frontGroundRoot == null)
            {
                Debug.LogWarning("[ForegroundOverlaySeamFixer] FrontGround container was not found.");
                return;
            }

            var renderers = frontGroundRoot.GetComponentsInChildren<SpriteRenderer>(true);
            var liveTiles = renderers
                .Where(renderer => renderer != null && renderer.name.StartsWith("DarkVignetteOverlay_", StringComparison.Ordinal))
                .Where(renderer => renderer.name != "DarkVignetteOverlay_Wide")
                .Where(renderer => !renderer.name.StartsWith("DarkVignetteOverlay_SourceTile_", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();

            var sourceTiles = renderers
                .Where(renderer => renderer != null && renderer.name.StartsWith("DarkVignetteOverlay_SourceTile_", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();

            var existingWide = renderers.FirstOrDefault(renderer => renderer != null && renderer.name == "DarkVignetteOverlay_Wide");
            var boundsSource = sourceTiles.Length > 0
                ? sourceTiles
                : liveTiles.Length > 0
                    ? liveTiles
                    : existingWide != null
                        ? new[] { existingWide }
                        : Array.Empty<SpriteRenderer>();

            if (boundsSource.Length == 0)
            {
                Debug.LogWarning("[ForegroundOverlaySeamFixer] No DarkVignetteOverlay sprite renderers were found.");
                return;
            }

            var overlaySprite = AssetDatabase.LoadAssetAtPath<Sprite>(overlayPath);
            if (overlaySprite == null)
            {
                Debug.LogWarning("[ForegroundOverlaySeamFixer] Overlay sprite was not found at " + overlayPath + ".");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(frontGroundRoot, "Fix foreground overlay seams");

            var combinedBounds = boundsSource[0].bounds;
            foreach (var tile in boundsSource.Skip(1))
            {
                combinedBounds.Encapsulate(tile.bounds);
            }

            var wideRenderer = existingWide ?? liveTiles.FirstOrDefault() ?? boundsSource[0];
            var wideTransform = wideRenderer.transform;
            RenameObject(wideTransform, "DarkVignetteOverlay_Wide");
            wideTransform.SetParent(frontGroundRoot.transform, true);

            wideRenderer.sprite = overlaySprite;
            wideRenderer.drawMode = SpriteDrawMode.Simple;
            wideRenderer.enabled = true;
            wideRenderer.sortingOrder = renderers
                .Where(renderer => renderer != null)
                .Select(renderer => renderer.sortingOrder)
                .DefaultIfEmpty(wideRenderer.sortingOrder)
                .Max();

            var spriteSize = overlaySprite.bounds.size;
            var targetWidth = Mathf.Max(MinimumWorldWidth, combinedBounds.size.x + HorizontalOverscan);
            var targetHeight = Mathf.Max(MinimumWorldHeight, combinedBounds.size.y * VerticalScaleMultiplier);
            wideTransform.position = new Vector3(combinedBounds.center.x, combinedBounds.center.y, wideTransform.position.z);
            wideTransform.localScale = new Vector3(
                targetWidth / Mathf.Max(0.001f, spriteSize.x),
                targetHeight / Mathf.Max(0.001f, spriteSize.y),
                wideTransform.localScale.z);

            var sourceTileContainer = EnsureChild(frontGroundRoot.transform, "DarkVignetteOverlay_SourceTiles_Disabled").transform;
            sourceTileContainer.gameObject.SetActive(false);

            var tileNumber = 1;
            foreach (var tile in liveTiles)
            {
                if (tile == wideRenderer)
                {
                    continue;
                }

                RenameObject(tile.transform, "DarkVignetteOverlay_SourceTile_" + tileNumber.ToString("00"));
                tileNumber++;
                MoveTransform(tile.transform, sourceTileContainer);
                tile.enabled = false;
                EditorUtility.SetDirty(tile);
            }

            wideTransform.SetSiblingIndex(0);
            sourceTileContainer.SetSiblingIndex(Math.Min(1, frontGroundRoot.transform.childCount - 1));

            EditorUtility.SetDirty(wideRenderer);
            EditorUtility.SetDirty(wideTransform.gameObject);
            EditorUtility.SetDirty(sourceTileContainer.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[ForegroundOverlaySeamFixer] Replaced " + liveTiles.Length + " foreground overlay tiles with one wide overlay using " + overlayPath + ".");
        }

        private static void ConfigureOverlayTextureImporter(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
            {
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                changed = true;
            }

            changed |= ConfigurePlatform(importer, "DefaultTexturePlatform");
            changed |= ConfigurePlatform(importer, "Standalone");
            changed |= ConfigurePlatform(importer, "Android");

            if (!changed)
            {
                return;
            }

            importer.SaveAndReimport();
        }

        private static bool ConfigurePlatform(TextureImporter importer, string buildTarget)
        {
            var settings = importer.GetPlatformTextureSettings(buildTarget);
            var changed = settings.maxTextureSize != 4096 ||
                          settings.format != TextureImporterFormat.RGBA32 ||
                          settings.textureCompression != TextureImporterCompression.Uncompressed ||
                          settings.crunchedCompression ||
                          settings.overridden != (buildTarget != "DefaultTexturePlatform");

            settings.name = buildTarget;
            settings.overridden = buildTarget != "DefaultTexturePlatform";
            settings.maxTextureSize = 4096;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            importer.SetPlatformTextureSettings(settings);
            return changed;
        }

        private static Scene OpenStageOneScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == ScenePath)
            {
                return activeScene;
            }

            if (activeScene.IsValid() && activeScene.isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("[ForegroundOverlaySeamFixer] Skipped because the active scene has unsaved changes.");
                    return default;
                }
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static GameObject FindObject(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(transform => transform.name == objectName)
                ?.gameObject;
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create foreground source tile container");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void MoveTransform(Transform transform, Transform parent)
        {
            if (transform.parent == parent)
            {
                return;
            }

            Undo.SetTransformParent(transform, parent, "Move foreground source tile");
            transform.SetParent(parent, true);
            EditorUtility.SetDirty(transform.gameObject);
        }

        private static void RenameObject(Transform transform, string cleanName)
        {
            if (transform.name == cleanName)
            {
                return;
            }

            Undo.RecordObject(transform.gameObject, "Rename foreground overlay object");
            transform.name = cleanName;
            EditorUtility.SetDirty(transform.gameObject);
        }

        private static void DeleteRequestFiles()
        {
            var requestPath = ProjectRelativeFilePath(RequestPath);
            if (File.Exists(requestPath))
            {
                File.Delete(requestPath);
            }

            var metaPath = ProjectRelativeFilePath(RequestPath + ".meta");
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        private static string ProjectRelativeFilePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
