using System;
using System.IO;
using System.Linq;
using NumiDream.StageOne.Respawn;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class RespawnSetup
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/RespawnSetup.request";
        static RespawnSetup()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    SetupRespawnPoints();
                }
                finally
                {
                    DeleteRequestFiles();
                    AssetDatabase.Refresh();
                }
            };
        }

        private static void SetupFromMenu()
        {
            SetupRespawnPoints();
        }

        private static void SetupRespawnPoints()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RespawnSetup] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[RespawnSetup] No Player-tagged object was found.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(player, "Setup player respawn");
            var playerRespawn = EnsureComponent<PlayerRespawnController>(player);
            ConfigurePlayerRespawn(playerRespawn, player);

            var respawnPoints = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name.StartsWith("RespawnPoint_", StringComparison.Ordinal))
                .OrderBy(transform => transform.position.x)
                .ThenBy(transform => transform.name, StringComparer.Ordinal)
                .ToArray();

            if (respawnPoints.Length == 0)
            {
                Debug.LogWarning("[RespawnSetup] No RespawnPoint_* objects were found.");
                return;
            }

            foreach (var respawnPointTransform in respawnPoints)
            {
                Undo.RegisterFullObjectHierarchyUndo(respawnPointTransform.gameObject, "Setup respawn point");
                ConfigureRespawnPoint(respawnPointTransform.gameObject);
            }

            SetInitialRespawnPoint(playerRespawn, respawnPoints[0].GetComponent<RespawnPoint>());

            EditorUtility.SetDirty(playerRespawn);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[RespawnSetup] Configured " + respawnPoints.Length + " respawn points and PlayerRespawnController.");
        }

        private static void ConfigurePlayerRespawn(PlayerRespawnController playerRespawn, GameObject player)
        {
            var body = player.GetComponent<Rigidbody2D>();
            var spriteRenderers = player.GetComponentsInChildren<SpriteRenderer>(true);

            var serializedRespawn = new SerializedObject(playerRespawn);
            serializedRespawn.FindProperty("fallYThreshold").floatValue = -8f;
            serializedRespawn.FindProperty("respawnOffset").vector2Value = new Vector2(0f, 0.35f);
            serializedRespawn.FindProperty("keepCurrentZ").boolValue = true;
            serializedRespawn.FindProperty("blinkDuration").floatValue = 2f;
            serializedRespawn.FindProperty("blinkInterval").floatValue = 0.12f;
            serializedRespawn.FindProperty("body").objectReferenceValue = body;

            var renderers = serializedRespawn.FindProperty("renderersToBlink");
            renderers.arraySize = spriteRenderers.Length;
            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = spriteRenderers[i];
            }

            serializedRespawn.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerRespawn);
        }

        private static void ConfigureRespawnPoint(GameObject target)
        {
            var collider = target.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = Undo.AddComponent<BoxCollider2D>(target);
            }

            collider.isTrigger = true;
            collider.size = new Vector2(1.25f, 3f);
            collider.offset = new Vector2(0f, 1.2f);
            EditorUtility.SetDirty(collider);

            var respawnPoint = EnsureComponent<RespawnPoint>(target);
            EditorUtility.SetDirty(respawnPoint);
            EditorUtility.SetDirty(target);
        }

        private static void SetInitialRespawnPoint(PlayerRespawnController playerRespawn, RespawnPoint firstPoint)
        {
            var serializedRespawn = new SerializedObject(playerRespawn);
            serializedRespawn.FindProperty("currentRespawnPoint").objectReferenceValue = firstPoint;
            serializedRespawn.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(playerRespawn);
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static Scene OpenStageOneScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == ScenePath)
            {
                return activeScene;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
