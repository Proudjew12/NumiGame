using System.IO;
using NumiDream.StageOne.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class PuzzleTwoWheelSetup
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/PuzzleTwoWheelSetup.request";
        private const string DestinationName = "SpinWheelDestination";
        private const string WaypointNamePrefix = "SpinWheelWaypoint_";
        private const string FallTriggerName = "FallTrigger";
        private const float ManualPushSeconds = 5f;
        private const float ReleasedFallSeconds = 1.6f;

        static PuzzleTwoWheelSetup()
        {

            if (!File.Exists(RequestPath))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    SetupPuzzleTwoWheel();
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
            SetupPuzzleTwoWheel();
        }

        private static void SetupPuzzleTwoWheel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleTwoWheelSetup] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var wheel = GameObject.Find("SpinWheel");
            if (wheel == null)
            {
                Debug.LogWarning("[PuzzleTwoWheelSetup] SpinWheel was not found in SceneStageOne.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(wheel, "Setup Puzzle Two Wheel");

            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                wheel.layer = groundLayer;
            }

            var rigidbody2D = wheel.GetComponent<Rigidbody2D>();
            if (rigidbody2D == null)
            {
                rigidbody2D = Undo.AddComponent<Rigidbody2D>(wheel);
            }

            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider2D = wheel.GetComponent<Collider2D>();
            if (collider2D == null)
            {
                var circle = Undo.AddComponent<CircleCollider2D>(wheel);
                var spriteRenderer = wheel.GetComponent<SpriteRenderer>();
                circle.radius = spriteRenderer != null && spriteRenderer.sprite != null
                    ? Mathf.Max(spriteRenderer.sprite.bounds.extents.x, spriteRenderer.sprite.bounds.extents.y) * 0.94f
                    : 2.5f;
                collider2D = circle;
            }

            collider2D.isTrigger = false;

            var puzzle = wheel.GetComponent<BicycleWheelPuzzle>();
            var createdPuzzle = false;
            if (puzzle == null)
            {
                puzzle = Undo.AddComponent<BicycleWheelPuzzle>(wheel);
                createdPuzzle = true;
            }

            var destination = GetOrCreateDestination(wheel);
            var waypointPath = GetOrCreateWaypointPath(wheel, destination);
            var player = GameObject.FindWithTag("Player");
            var serializedPuzzle = new SerializedObject(puzzle);
            serializedPuzzle.FindProperty("destinationPoint").objectReferenceValue = destination != null ? destination.transform : null;

            var pathWaypoints = serializedPuzzle.FindProperty("pathWaypoints");
            if (!HasAssignedWaypoints(pathWaypoints))
            {
                SetWaypointPath(pathWaypoints, waypointPath);
            }

            serializedPuzzle.FindProperty("useCustomGroundedLocalPosition").boolValue = true;
            serializedPuzzle.FindProperty("groundedLocalPosition").vector3Value = GetLocalDestinationPosition(wheel, destination);
            serializedPuzzle.FindProperty("requiredSpinSeconds").floatValue = ManualPushSeconds;
            serializedPuzzle.FindProperty("releasedFallSeconds").floatValue = ReleasedFallSeconds;
            serializedPuzzle.FindProperty("requireInputToMove").boolValue = true;
            serializedPuzzle.FindProperty("autoSpinAfterPathComplete").boolValue = true;

            if (createdPuzzle)
            {
                serializedPuzzle.FindProperty("player").objectReferenceValue = player != null ? player.transform : null;
                serializedPuzzle.FindProperty("playerTag").stringValue = "Player";
                serializedPuzzle.FindProperty("requirePlayerInRange").boolValue = true;
                serializedPuzzle.FindProperty("activationDistance").floatValue = 7.5f;
                serializedPuzzle.FindProperty("fallArcHeight").floatValue = 0.25f;
                serializedPuzzle.FindProperty("manualSpinDegreesPerSecond").floatValue = 500f;
                serializedPuzzle.FindProperty("autoSpinDegreesPerSecond").floatValue = 280f;
                serializedPuzzle.FindProperty("spinDirection").intValue = -1;
                serializedPuzzle.FindProperty("carryPlayerOnContact").boolValue = true;
                serializedPuzzle.FindProperty("ridePushSpeed").floatValue = 3.2f;
                serializedPuzzle.FindProperty("ridePushForce").floatValue = 12f;
            }

            serializedPuzzle.ApplyModifiedPropertiesWithoutUndo();
            ConfigureFallTrigger(puzzle);

            EditorUtility.SetDirty(wheel);
            EditorUtility.SetDirty(puzzle);
            if (destination != null)
            {
                EditorUtility.SetDirty(destination);
            }

            foreach (var waypoint in waypointPath)
            {
                if (waypoint != null)
                {
                    EditorUtility.SetDirty(waypoint);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[PuzzleTwoWheelSetup] SpinWheel is ready. Move SpinWheelWaypoint_01, SpinWheelWaypoint_02, and SpinWheelDestination to control the wheel path.");
        }

        private static void ConfigureFallTrigger(BicycleWheelPuzzle puzzle)
        {
            var fallTrigger = GameObject.Find(FallTriggerName);
            if (fallTrigger == null)
            {
                Debug.LogWarning("[PuzzleTwoWheelSetup] FallTrigger was not found. Create a trigger named FallTrigger at the ground edge to release the wheel.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(fallTrigger, "Setup Puzzle Two Wheel Fall Trigger");

            var collider2D = fallTrigger.GetComponent<Collider2D>();
            if (collider2D == null)
            {
                collider2D = Undo.AddComponent<BoxCollider2D>(fallTrigger);
            }

            collider2D.isTrigger = true;

            var fallTriggerScript = fallTrigger.GetComponent<BicycleWheelFallTrigger>();
            if (fallTriggerScript == null)
            {
                fallTriggerScript = Undo.AddComponent<BicycleWheelFallTrigger>(fallTrigger);
            }

            var serializedTrigger = new SerializedObject(fallTriggerScript);
            serializedTrigger.FindProperty("wheel").objectReferenceValue = puzzle;
            serializedTrigger.FindProperty("triggerOnce").boolValue = true;
            serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(fallTrigger);
            EditorUtility.SetDirty(collider2D);
            EditorUtility.SetDirty(fallTriggerScript);
        }

        private static GameObject GetOrCreateDestination(GameObject wheel)
        {
            var destination = GameObject.Find(DestinationName);
            if (destination == null)
            {
                destination = new GameObject(DestinationName);
                Undo.RegisterCreatedObjectUndo(destination, "Create Puzzle Two Wheel Destination");

                if (wheel.transform.parent != null)
                {
                    destination.transform.SetParent(wheel.transform.parent, worldPositionStays: false);
                    destination.transform.localPosition = wheel.transform.localPosition + new Vector3(2.2f, -1.25f, 0f);
                }
                else
                {
                    destination.transform.position = wheel.transform.position + new Vector3(2.2f, -1.25f, 0f);
                }
            }

            if (destination.GetComponent<BicycleWheelTargetPoint>() == null)
            {
                Undo.AddComponent<BicycleWheelTargetPoint>(destination);
            }

            return destination;
        }

        private static Transform[] GetOrCreateWaypointPath(GameObject wheel, GameObject destination)
        {
            var startLocal = wheel.transform.localPosition;
            var destinationLocal = GetLocalDestinationPosition(wheel, destination);

            var firstLocal = Vector3.Lerp(startLocal, destinationLocal, 0.35f) + new Vector3(0f, 0.25f, 0f);
            var secondLocal = Vector3.Lerp(startLocal, destinationLocal, 0.7f) + new Vector3(0f, -0.15f, 0f);

            var first = GetOrCreateWaypoint(wheel, WaypointNamePrefix + "01", firstLocal);
            var second = GetOrCreateWaypoint(wheel, WaypointNamePrefix + "02", secondLocal);

            return new[]
            {
                first != null ? first.transform : null,
                second != null ? second.transform : null,
                destination != null ? destination.transform : null
            };
        }

        private static GameObject GetOrCreateWaypoint(GameObject wheel, string name, Vector3 localPosition)
        {
            var waypoint = GameObject.Find(name);
            if (waypoint == null)
            {
                waypoint = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(waypoint, "Create Puzzle Two Wheel Waypoint");

                if (wheel.transform.parent != null)
                {
                    waypoint.transform.SetParent(wheel.transform.parent, worldPositionStays: false);
                    waypoint.transform.localPosition = localPosition;
                }
                else
                {
                    waypoint.transform.position = localPosition;
                }
            }

            if (waypoint.GetComponent<BicycleWheelTargetPoint>() == null)
            {
                Undo.AddComponent<BicycleWheelTargetPoint>(waypoint);
            }

            return waypoint;
        }

        private static bool HasAssignedWaypoints(SerializedProperty pathWaypoints)
        {
            if (pathWaypoints == null)
            {
                return false;
            }

            for (var i = 0; i < pathWaypoints.arraySize; i++)
            {
                if (pathWaypoints.GetArrayElementAtIndex(i).objectReferenceValue != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetWaypointPath(SerializedProperty pathWaypoints, Transform[] waypointPath)
        {
            if (pathWaypoints == null || waypointPath == null)
            {
                return;
            }

            pathWaypoints.arraySize = waypointPath.Length;
            for (var i = 0; i < waypointPath.Length; i++)
            {
                pathWaypoints.GetArrayElementAtIndex(i).objectReferenceValue = waypointPath[i];
            }
        }

        private static Vector3 GetLocalDestinationPosition(GameObject wheel, GameObject destination)
        {
            if (destination == null)
            {
                return wheel.transform.localPosition + new Vector3(2.2f, -1.25f, 0f);
            }

            return wheel.transform.parent != null
                ? wheel.transform.parent.InverseTransformPoint(destination.transform.position)
                : destination.transform.position;
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
                    Debug.LogWarning("[PuzzleTwoWheelSetup] Skipped because the active scene has unsaved changes.");
                    return default;
                }
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
