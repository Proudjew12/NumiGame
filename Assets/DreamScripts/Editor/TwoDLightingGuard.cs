using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class TwoDLightingGuard
    {
        private const string RootPath = "DreamScripts/Project";
        private const int RootMenuPriorityBase = 60041;
        private const string SessionKey = "DreamScripts.EditorTools.TwoDLightingGuard.Checked";
        private const string LitSpriteMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
        private const string UnlitSpriteMaterialGuid = "9dfc825aed78fcd4ba02077103263b40";
        private const string PlayerPrefabPath = "Assets/PreFab/Player.prefab";

        private static readonly string[] ScenePaths =
        {
            "Assets/NumiDream/StageOne/SceneStageOne.unity",
            "Assets/NumiDream/StageOne/Stage2.unity"
        };

        static TwoDLightingGuard()
        {
            DreamScriptRegistry.Register("Project/Fix 2D Lighting", Fix2DLightingFromMenu, priority: 41);
            EditorApplication.delayCall += Ensure2DLightingOnStartup;
        }

        [MenuItem(RootPath + "/Fix 2D Lighting", false, RootMenuPriorityBase)]
        private static void Fix2DLightingFromMenu()
        {
            Ensure2DLighting(showDialog: true, allowScenePrompt: true);
        }

        private static void Ensure2DLightingOnStartup()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Ensure2DLightingOnStartup;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            Ensure2DLighting(showDialog: false, allowScenePrompt: false);
        }

        private static void Ensure2DLighting(bool showDialog, bool allowScenePrompt)
        {
            var litMaterial = LoadLitSpriteMaterial();
            if (litMaterial == null)
            {
                const string message = "Could not find the URP Sprite-Lit default material.";
                Debug.LogError("[DreamScripts] " + message);

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Fix 2D Lighting", message, "OK");
                }

                return;
            }

            if (allowScenePrompt && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var openScenePaths = GetOpenScenePaths();
            var activeScenePath = EditorSceneManager.GetActiveScene().path;
            var summary = new RepairSummary();

            try
            {
                summary.SpriteRenderersUpdated += FixPlayerPrefab(litMaterial);

                foreach (var scenePath in ScenePaths)
                {
                    summary.Combine(FixScene(scenePath, litMaterial));
                }

                if (summary.Changed)
                {
                    AssetDatabase.SaveAssets();
                    Debug.Log(
                        "[DreamScripts] 2D lighting fixed: updated " + summary.SpriteRenderersUpdated +
                        " sprite renderer material assignments and created " + summary.GlobalLightsCreated +
                        " Global Light 2D object(s).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[DreamScripts] Failed to fix 2D lighting.\n" + ex);

                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Fix 2D Lighting", "The 2D lighting repair failed. Check the Console for details.", "OK");
                }

                return;
            }
            finally
            {
                RestoreOpenScenes(openScenePaths, activeScenePath);
            }

            if (showDialog)
            {
                var message = summary.Changed
                    ? "Fixed 2D lighting.\n\nUpdated sprite renderers: " + summary.SpriteRenderersUpdated +
                      "\nCreated Global Light 2D objects: " + summary.GlobalLightsCreated
                    : "2D lighting was already configured.";

                EditorUtility.DisplayDialog("Fix 2D Lighting", message, "OK");
            }
        }

        private static Material LoadLitSpriteMaterial()
        {
            var litMaterialPath = AssetDatabase.GUIDToAssetPath(LitSpriteMaterialGuid);
            if (string.IsNullOrWhiteSpace(litMaterialPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Material>(litMaterialPath);
        }

        private static int FixPlayerPrefab(Material litMaterial)
        {
            if (!File.Exists(ProjectRelativeFilePath(PlayerPrefabPath)))
            {
                return 0;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var updatedCount = ApplyLitMaterial(prefabRoot.GetComponentsInChildren<SpriteRenderer>(true), litMaterial);
                if (updatedCount > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
                }

                return updatedCount;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static RepairSummary FixScene(string scenePath, Material litMaterial)
        {
            var summary = new RepairSummary();
            if (!File.Exists(ProjectRelativeFilePath(scenePath)))
            {
                return summary;
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            summary.SpriteRenderersUpdated += ApplyLitMaterial(
                scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true)),
                litMaterial);

            if (EnsureGlobalLight2D(scene))
            {
                summary.GlobalLightsCreated++;
            }

            if (summary.Changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            return summary;
        }

        private static int ApplyLitMaterial(System.Collections.Generic.IEnumerable<SpriteRenderer> spriteRenderers, Material litMaterial)
        {
            var updatedCount = 0;

            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer == null || !NeedsLitMaterial(spriteRenderer.sharedMaterial, litMaterial))
                {
                    continue;
                }

                spriteRenderer.sharedMaterial = litMaterial;
                EditorUtility.SetDirty(spriteRenderer);
                updatedCount++;
            }

            return updatedCount;
        }

        private static bool NeedsLitMaterial(Material currentMaterial, Material litMaterial)
        {
            if (currentMaterial == litMaterial)
            {
                return false;
            }

            if (currentMaterial == null)
            {
                return true;
            }

            var materialPath = AssetDatabase.GetAssetPath(currentMaterial);
            if (!string.IsNullOrWhiteSpace(materialPath))
            {
                var materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
                if (string.Equals(materialGuid, UnlitSpriteMaterialGuid, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return currentMaterial.shader != null &&
                   string.Equals(currentMaterial.shader.name, "Universal Render Pipeline/2D/Sprite-Unlit-Default", StringComparison.Ordinal);
        }

        private static bool EnsureGlobalLight2D(Scene scene)
        {
            var hasGlobalLight = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Light2D>(true))
                .Any(light => light.lightType == Light2D.LightType.Global);

            if (hasGlobalLight)
            {
                return false;
            }

            var globalLightObject = new GameObject("Global Light 2D");
            SceneManager.MoveGameObjectToScene(globalLightObject, scene);

            var light = globalLightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;

            EditorUtility.SetDirty(globalLightObject);
            EditorUtility.SetDirty(light);
            return true;
        }

        private static string[] GetOpenScenePaths()
        {
            return Enumerable.Range(0, EditorSceneManager.sceneCount)
                .Select(EditorSceneManager.GetSceneAt)
                .Where(scene => scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static void RestoreOpenScenes(string[] scenePaths, string activeScenePath)
        {
            if (scenePaths == null || scenePaths.Length == 0)
            {
                return;
            }

            var loadedCount = 0;
            var activeScene = default(Scene);

            foreach (var scenePath in scenePaths)
            {
                if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(ProjectRelativeFilePath(scenePath)))
                {
                    continue;
                }

                var mode = loadedCount == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive;
                var scene = EditorSceneManager.OpenScene(scenePath, mode);
                loadedCount++;

                if (string.Equals(scenePath, activeScenePath, StringComparison.Ordinal))
                {
                    activeScene = scene;
                }
            }

            if (activeScene.IsValid())
            {
                EditorSceneManager.SetActiveScene(activeScene);
            }
        }

        private static string ProjectRelativeFilePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private struct RepairSummary
        {
            public int SpriteRenderersUpdated;
            public int GlobalLightsCreated;

            public bool Changed => SpriteRenderersUpdated > 0 || GlobalLightsCreated > 0;

            public void Combine(RepairSummary other)
            {
                SpriteRenderersUpdated += other.SpriteRenderersUpdated;
                GlobalLightsCreated += other.GlobalLightsCreated;
            }
        }
    }
}
