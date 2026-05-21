using System.IO;
using System.Linq;
using NumiDream.StageOne.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class StoneHandsBridgeLiftSetup
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/StoneHandsBridgeLiftSetup.request";
        static StoneHandsBridgeLiftSetup()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    SetupBridgeLift();
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
            SetupBridgeLift();
        }

        private static void SetupBridgeLift()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[StoneHandsBridgeLiftSetup] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var bellRope = FindObject(scene, "BellRope");
            var bridge = FindObject(scene, "StoneHandsBridge_Final");
            var player = GameObject.FindWithTag("Player");
            var groundCheck = player != null ? player.transform.Find("GroundCheck") : null;

            if (bellRope == null || bridge == null || player == null || groundCheck == null)
            {
                Debug.LogWarning("[StoneHandsBridgeLiftSetup] Needed BellRope, StoneHandsBridge_Final, Player, and Player/GroundCheck in the scene.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(bellRope, "Setup Stone Hands Bridge Lift");

            var lift = bellRope.GetComponent<StoneHandsBridgeLift>();
            var created = lift == null;
            if (lift == null)
            {
                lift = Undo.AddComponent<StoneHandsBridgeLift>(bellRope);
            }

            var serializedLift = new SerializedObject(lift);
            serializedLift.FindProperty("stoneHandsBridge").objectReferenceValue = bridge.transform;
            serializedLift.FindProperty("bellRope").objectReferenceValue = bellRope.transform;
            serializedLift.FindProperty("playerGroundCheck").objectReferenceValue = groundCheck;
            serializedLift.FindProperty("player").objectReferenceValue = player.transform;
            serializedLift.FindProperty("bridgeCollider").objectReferenceValue = bridge.GetComponent<Collider2D>();
            serializedLift.FindProperty("bridgeRenderer").objectReferenceValue = bridge.GetComponent<Renderer>();

            if (created)
            {
                serializedLift.FindProperty("requirePlayerInRange").boolValue = false;
                serializedLift.FindProperty("playerTag").stringValue = "Player";
                serializedLift.FindProperty("activationDistance").floatValue = 5f;
                serializedLift.FindProperty("bridgeRiseSpeed").floatValue = 0.85f;
                serializedLift.FindProperty("ropeDropPerBridgeUnit").floatValue = 0.75f;
                serializedLift.FindProperty("stopBelowGroundCheck").floatValue = 0.03f;
                serializedLift.FindProperty("fallbackMaxBridgeRise").floatValue = 6f;
            }

            serializedLift.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(bellRope);
            EditorUtility.SetDirty(lift);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[StoneHandsBridgeLiftSetup] BellRope now raises StoneHandsBridge_Final while T is held, then locks when it reaches Player/GroundCheck.");
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
                    Debug.LogWarning("[StoneHandsBridgeLiftSetup] Skipped because the active scene has unsaved changes.");
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
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }
    }
}
