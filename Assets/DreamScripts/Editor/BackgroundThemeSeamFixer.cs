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
    internal static class BackgroundThemeSeamFixer
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/BackgroundThemeSeamFixer.request";
        private const string SkyGradientPath = "Assets/NumiDream/StageOne/Art/ParallaxLayers/001_Back_SkyGradient.png";
        private const string SeamlessSkyGradientPath = "Assets/NumiDream/StageOne/Art/ParallaxLayers/001_Back_SkyGradient_SeamlessWide.png";
        private const float HorizontalOverscan = 1.25f;
        private const float VerticalScaleMultiplier = 2.2f;
        private const float MinimumWorldWidth = 270f;
        private const float MinimumWorldHeight = 52f;

        static BackgroundThemeSeamFixer()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    FixBackgroundThemeSeams();
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
            FixBackgroundThemeSeams();
        }

        private static void FixBackgroundThemeSeams()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BackgroundThemeSeamFixer] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var skyPath = File.Exists(ProjectRelativeFilePath(SeamlessSkyGradientPath))
                ? SeamlessSkyGradientPath
                : SkyGradientPath;

            ConfigureSkyTextureImporter(skyPath);
            AssetDatabase.ImportAsset(skyPath, ImportAssetOptions.ForceUpdate);

            var backgroundRoot = FindObject(scene, "Background");
            if (backgroundRoot == null)
            {
                Debug.LogWarning("[BackgroundThemeSeamFixer] Background container was not found.");
                return;
            }

            var renderers = backgroundRoot.GetComponentsInChildren<SpriteRenderer>(true);
            var themes = renderers
                .Where(renderer => renderer != null && renderer.name.StartsWith("BackgroundTheme_", StringComparison.Ordinal))
                .Where(renderer => !renderer.name.StartsWith("BackgroundTheme_SourceTile_", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();

            if (themes.Length == 0)
            {
                Debug.LogWarning("[BackgroundThemeSeamFixer] No BackgroundTheme_* sprite renderers were found.");
                return;
            }

            var sourceTiles = renderers
                .Where(renderer => renderer != null && renderer.name.StartsWith("BackgroundTheme_SourceTile_", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();

            var boundsSource = sourceTiles.Concat(themes).ToArray();
            var skySprite = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
            if (skySprite == null)
            {
                Debug.LogWarning("[BackgroundThemeSeamFixer] Sky sprite was not found at " + skyPath + ".");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(backgroundRoot, "Fix Background Theme Seams");

            var combinedBounds = boundsSource[0].bounds;
            foreach (var theme in boundsSource.Skip(1))
            {
                combinedBounds.Encapsulate(theme.bounds);
            }

            var wideRenderer = themes.FirstOrDefault(renderer => renderer.name == "BackgroundTheme_Wide") ?? themes[0];
            var wideTransform = wideRenderer.transform;
            RenameObject(wideTransform, "BackgroundTheme_Wide");
            wideRenderer.sprite = skySprite;
            wideRenderer.drawMode = SpriteDrawMode.Simple;
            wideRenderer.sortingOrder = -1000;
            wideRenderer.enabled = true;

            var spriteSize = skySprite.bounds.size;
            var targetWidth = Mathf.Max(MinimumWorldWidth, combinedBounds.size.x + HorizontalOverscan);
            var targetHeight = Mathf.Max(MinimumWorldHeight, combinedBounds.size.y * VerticalScaleMultiplier);
            wideTransform.position = new Vector3(combinedBounds.center.x, combinedBounds.center.y, wideTransform.position.z);
            wideTransform.localScale = new Vector3(
                targetWidth / Mathf.Max(0.001f, spriteSize.x),
                targetHeight / Mathf.Max(0.001f, spriteSize.y),
                wideTransform.localScale.z);

            var sourceTileContainer = EnsureChild(backgroundRoot.transform, "BackgroundTheme_SourceTiles_Disabled").transform;
            sourceTileContainer.gameObject.SetActive(false);

            var tileNumber = 1;
            foreach (var theme in themes)
            {
                if (theme == wideRenderer)
                {
                    continue;
                }

                RenameObject(theme.transform, "BackgroundTheme_SourceTile_" + tileNumber.ToString("00"));
                tileNumber++;
                MoveTransform(theme.transform, sourceTileContainer);
                theme.enabled = false;
                EditorUtility.SetDirty(theme);
            }

            wideTransform.SetSiblingIndex(0);
            sourceTileContainer.SetSiblingIndex(Math.Min(1, backgroundRoot.transform.childCount - 1));

            EditorUtility.SetDirty(wideRenderer);
            EditorUtility.SetDirty(wideTransform.gameObject);
            EditorUtility.SetDirty(sourceTileContainer.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[BackgroundThemeSeamFixer] Replaced " + themes.Length + " background theme tiles with one wide seamless background using " + skyPath + ".");
        }

        private static void ConfigureSkyTextureImporter(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var changed = false;
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
                    Debug.LogWarning("[BackgroundThemeSeamFixer] Skipped because the active scene has unsaved changes.");
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
            Undo.RegisterCreatedObjectUndo(child, "Create background source tile container");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void MoveTransform(Transform transform, Transform parent)
        {
            if (transform.parent == parent)
            {
                return;
            }

            Undo.SetTransformParent(transform, parent, "Move background source tile");
            transform.SetParent(parent, true);
            EditorUtility.SetDirty(transform.gameObject);
        }

        private static void RenameObject(Transform transform, string cleanName)
        {
            if (transform.name == cleanName)
            {
                return;
            }

            Undo.RecordObject(transform.gameObject, "Rename background theme object");
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
