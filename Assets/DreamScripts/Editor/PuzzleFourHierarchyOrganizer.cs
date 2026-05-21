using System;
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
    internal static class PuzzleFourHierarchyOrganizer
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/PuzzleFourHierarchyOrganizer.request";
        static PuzzleFourHierarchyOrganizer()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    OrganizePuzzleFour();
                }
                finally
                {
                    DeleteRequestFiles();
                    AssetDatabase.Refresh();
                }
            };
        }

        private static void OrganizeFromMenu()
        {
            OrganizePuzzleFour();
        }

        private static void OrganizePuzzleFour()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PuzzleFourHierarchyOrganizer] Skipped while Unity is entering or in Play Mode.");
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
                Debug.LogWarning("[PuzzleFourHierarchyOrganizer] PuzzleFour was not found.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(puzzleFour, "Organize Puzzle Four hierarchy");

            var upDownRoot = FindDirectChildByAnyName(
                puzzleFour.transform,
                "Up_Down_Islands",
                "UP_Down_island",
                "Up_Down_island",
                "Up_Down_Island",
                "up_down_island",
                "UpDownIsland",
                "UpDownIslands");

            var leftRightRoot = FindDirectChildByAnyName(
                puzzleFour.transform,
                "Left_Right_Rotation",
                "Left_right_rotation",
                "Left_Right_rotation",
                "LeftRightRotation",
                "Left_Right_Islands");

            upDownRoot = EnsureDirectChild(puzzleFour.transform, upDownRoot, "Up_Down_Islands").transform;
            leftRightRoot = EnsureDirectChild(puzzleFour.transform, leftRightRoot, "Left_Right_Rotation").transform;

            var upDownIslands = upDownRoot.Cast<Transform>()
                .Where(IsIslandVisual)
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ToArray();

            for (var i = 0; i < upDownIslands.Length; i++)
            {
                RenameObject(upDownIslands[i], "UpDownIsland_" + (i + 1).ToString("00"));
                ConfigureFutureVerticalIsland(upDownIslands[i].gameObject);
                upDownIslands[i].SetSiblingIndex(i);
            }

            var leftRightIslands = leftRightRoot.Cast<Transform>()
                .Where(IsIslandVisual)
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ToArray();

            for (var i = 0; i < leftRightIslands.Length; i++)
            {
                RenameObject(leftRightIslands[i], "LeftRightIsland_" + (i + 1).ToString("00"));
                leftRightIslands[i].SetSiblingIndex(i);
            }

            SetSiblingOrder(puzzleFour.transform, "Up_Down_Islands", "Left_Right_Rotation", "RespawnPoint_Four");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PuzzleFourHierarchyOrganizer] Organized PuzzleFour with " + leftRightIslands.Length + " rotating islands and " + upDownIslands.Length + " future up/down islands.");
        }

        private static GameObject EnsureDirectChild(Transform parent, Transform existing, string cleanName)
        {
            if (existing != null)
            {
                if (existing.parent != parent)
                {
                    Undo.SetTransformParent(existing, parent, "Organize Puzzle Four hierarchy");
                    existing.SetParent(parent, true);
                }

                RenameObject(existing, cleanName);
                return existing.gameObject;
            }

            var child = new GameObject(cleanName);
            Undo.RegisterCreatedObjectUndo(child, "Create Puzzle Four container");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Transform FindDirectChildByAnyName(Transform parent, params string[] names)
        {
            foreach (Transform child in parent)
            {
                if (names.Any(name => string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsIslandVisual(Transform transform)
        {
            return transform != null && transform.GetComponent<SpriteRenderer>() != null;
        }

        private static void ConfigureFutureVerticalIsland(GameObject island)
        {
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
            {
                island.layer = groundLayer;
            }

            island.tag = "Ground";

            var body = island.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = Undo.AddComponent<Rigidbody2D>(island);
            }

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(island);
        }

        private static void RenameObject(Transform transform, string cleanName)
        {
            if (transform.name == cleanName)
            {
                return;
            }

            Undo.RecordObject(transform.gameObject, "Rename Puzzle Four object");
            transform.name = cleanName;
            EditorUtility.SetDirty(transform.gameObject);
        }

        private static void SetSiblingOrder(Transform parent, params string[] orderedNames)
        {
            var index = 0;
            foreach (var childName in orderedNames)
            {
                var child = parent.Find(childName);
                if (child == null)
                {
                    continue;
                }

                child.SetSiblingIndex(index);
                index++;
            }
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
                    Debug.LogWarning("[PuzzleFourHierarchyOrganizer] Skipped because the active scene has unsaved changes.");
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
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
