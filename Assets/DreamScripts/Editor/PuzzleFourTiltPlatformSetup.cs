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
    internal static class PuzzleFourTiltPlatformSetup
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/PuzzleFourTiltPlatformSetup.request";
        static PuzzleFourTiltPlatformSetup()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    SetupTiltPlatforms();
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
            SetupTiltPlatforms();
        }

        private static void SetupTiltPlatforms()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleFourTiltPlatformSetup] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var puzzleFour = FindObject(scene, "PuzzleFour");
            if (puzzleFour == null)
            {
                Debug.LogWarning("[PuzzleFourTiltPlatformSetup] PuzzleFour was not found.");
                return;
            }

            var leftRightRoot = puzzleFour.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "Left_Right_Rotation");
            var searchRoot = leftRightRoot != null ? leftRightRoot : puzzleFour.transform;

            var platforms = searchRoot.GetComponentsInChildren<Transform>(true)
                .Where(IsRotatingIslandCandidate)
                .OrderBy(child => child.position.x)
                .ToArray();

            if (platforms.Length == 0)
            {
                Debug.LogWarning("[PuzzleFourTiltPlatformSetup] No small island platforms were found.");
                return;
            }

            for (var i = 0; i < platforms.Length; i++)
            {
                ConfigurePlatform(platforms[i].gameObject, i);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PuzzleFourTiltPlatformSetup] Configured " + platforms.Length + " looping tilt platforms.");
        }

        private static void ConfigurePlatform(GameObject platform, int index)
        {
            Undo.RegisterFullObjectHierarchyUndo(platform, "Setup Puzzle Four tilt platform");

            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                platform.layer = groundLayer;
            }

            platform.tag = "Ground";

            var body = platform.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody2D>(platform);
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            EditorUtility.SetDirty(body);

            ConfigureWalkableCollider(platform);

            var tilt = platform.GetComponent<LoopingTiltPlatform2D>();
            if (tilt == null)
            {
                tilt = Undo.AddComponent<LoopingTiltPlatform2D>(platform);
            }

            var leftAngles = new[] { -26f, -34f, -28f, -36f, -30f };
            var rightAngles = new[] { 32f, 24f, 38f, 28f, 34f };
            var durations = new[] { 3.2f, 2.65f, 3.75f, 2.9f, 3.45f };
            var phases = new[] { 0.05f, 0.37f, 0.68f, 0.22f, 0.83f };

            var presetIndex = Mathf.Clamp(index, 0, leftAngles.Length - 1);
            var serializedTilt = new SerializedObject(tilt);
            serializedTilt.FindProperty("leftAngle").floatValue = leftAngles[presetIndex];
            serializedTilt.FindProperty("rightAngle").floatValue = rightAngles[presetIndex];
            serializedTilt.FindProperty("cycleDuration").floatValue = durations[presetIndex];
            serializedTilt.FindProperty("phaseOffset").floatValue = phases[presetIndex];
            serializedTilt.FindProperty("useLocalStartAngle").boolValue = true;
            serializedTilt.FindProperty("body").objectReferenceValue = body;
            serializedTilt.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(tilt);
            EditorUtility.SetDirty(platform);
        }

        private static bool IsRotatingIslandCandidate(Transform child)
        {
            if (child == null || child.GetComponent<SpriteRenderer>() == null)
            {
                return false;
            }

            return child.name.StartsWith("LeftRightIsland_", System.StringComparison.Ordinal) ||
                   child.name == "Rotate_Island" ||
                   child.name.StartsWith("Rotate_Island (", System.StringComparison.Ordinal) ||
                   child.name == "016_Front_GroundIsland_Right" ||
                   child.name.StartsWith("016_Front_GroundIsland_Right (", System.StringComparison.Ordinal);
        }

        private static void ConfigureWalkableCollider(GameObject platform)
        {
            var edgeCollider = platform.GetComponent<EdgeCollider2D>();
            if (edgeCollider == null)
            {
                edgeCollider = Undo.AddComponent<EdgeCollider2D>(platform);
            }

            edgeCollider.isTrigger = false;
            edgeCollider.offset = new Vector2(0f, 1.62f);
            edgeCollider.edgeRadius = 0f;
            edgeCollider.points = new[]
            {
                new Vector2(-3.8f, 0f),
                new Vector2(0f, -0.22f),
                new Vector2(3.8f, 0f),
            };
            edgeCollider.useAdjacentStartPoint = true;
            edgeCollider.useAdjacentEndPoint = true;
            edgeCollider.adjacentStartPoint = new Vector2(-4.2f, -0.65f);
            edgeCollider.adjacentEndPoint = new Vector2(4.2f, -0.65f);
            EditorUtility.SetDirty(edgeCollider);
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
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
