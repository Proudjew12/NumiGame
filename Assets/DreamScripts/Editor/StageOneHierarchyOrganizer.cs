using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NumiDream.StageOne;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class StageOneHierarchyOrganizer
    {
        private const string ScenePath = "Assets/NumiDream/StageOne/SceneStageOne.unity";
        private const string RequestPath = "Assets/DreamScripts/Editor/StageOneHierarchyOrganizer.request";
        static StageOneHierarchyOrganizer()
        {

            if (!File.Exists(ProjectRelativeFilePath(RequestPath)))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    OrganizeStageOneHierarchy();
                }
                finally
                {
                    var requestPath = ProjectRelativeFilePath(RequestPath);
                    if (File.Exists(requestPath))
                    {
                        File.Delete(requestPath);
                    }

                    var requestMetaPath = ProjectRelativeFilePath(RequestPath + ".meta");
                    if (File.Exists(requestMetaPath))
                    {
                        File.Delete(requestMetaPath);
                    }

                    AssetDatabase.Refresh();
                }
            };
        }

        private static void OrganizeFromMenu()
        {
            OrganizeStageOneHierarchy();
        }

        private static void OrganizeStageOneHierarchy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[StageOneHierarchyOrganizer] Skipped while Unity is entering or in Play Mode.");
                return;
            }

            var scene = OpenStageOneScene();
            if (!scene.IsValid())
            {
                return;
            }

            var root = FindRoot(scene, "Stage-One");
            if (root == null)
            {
                Debug.LogWarning("[StageOneHierarchyOrganizer] Stage-One root was not found.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Organize Stage-One Hierarchy");

            if (UsesSeparatorHierarchy(root.transform))
            {
                OrganizeSeparatorHierarchy(scene, root.transform);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log("[StageOneHierarchyOrganizer] Stage-One separator hierarchy organized.");
                return;
            }

            var environment = EnsureChild(root.transform, "Environment");
            var gameplay = EnsureChild(root.transform, "Gameplay");
            var puzzles = EnsureChild(root.transform, "Puzzles");
            var collectibles = EnsureChild(root.transform, "Collectibles");
            var ui = EnsureChild(root.transform, "UI");
            var systems = EnsureChild(root.transform, "Systems");

            MoveIfExists(scene, "Main Camera", systems.transform);
            MoveIfExists(scene, "StageOneManager", systems.transform);
            MoveIfExists(scene, "Player", gameplay.transform);

            OrganizeEnvironment(environment.transform);
            OrganizeStageVisuals(root.transform, environment.transform);
            OrganizeGameplay(scene, gameplay.transform);
            OrganizePuzzleOne(scene, puzzles.transform);
            OrganizePuzzleTwo(scene, puzzles.transform);
            OrganizePuzzleThree(scene, puzzles.transform);
            OrganizePuzzleFour(scene, puzzles.transform);
            OrganizeCollectibles(collectibles.transform);

            SetSiblingOrder(root.transform, "Environment", "Gameplay", "Puzzles", "Collectibles", "UI", "Systems");
            SetSiblingOrder(environment.transform, "Background", "Parallax", "Trees", "StageShell", "Floor", "FrontGround");
            SetSiblingOrder(gameplay.transform, "Player", "RespawnPoints");
            SetSiblingOrder(puzzles.transform, "PuzzleOne", "PuzzleTwo", "PuzzleThree", "PuzzleFour");
            SetSiblingOrder(systems.transform, "Main Camera", "StageOneManager");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[StageOneHierarchyOrganizer] Stage-One hierarchy organized.");
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
                    Debug.LogWarning("[StageOneHierarchyOrganizer] Skipped because the active scene has unsaved changes.");
                    return default;
                }
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static GameObject FindRoot(Scene scene, string rootName)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == rootName);
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var found = parent.Find(name);
            if (found != null)
            {
                return found.gameObject;
            }

            var child = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(child, "Create hierarchy folder");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static Transform FindOrCreateSection(Scene scene, Transform parent, string sectionName)
        {
            var section = parent.Find(sectionName);
            if (section != null)
            {
                return section;
            }

            MoveIfExists(scene, sectionName, parent);
            section = parent.Find(sectionName);
            return section != null ? section : EnsureChild(parent, sectionName).transform;
        }

        private static void MoveIfExists(Scene scene, string objectName, Transform newParent)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindByName(root.transform, objectName);
                if (found == null || found == newParent)
                {
                    continue;
                }

                Undo.SetTransformParent(found, newParent, "Organize Stage-One Hierarchy");
                found.SetParent(newParent, true);
                EditorUtility.SetDirty(found.gameObject);
                return;
            }
        }

        private static void MoveTransform(Transform found, Transform newParent)
        {
            if (found == null || found == newParent || found.parent == newParent)
            {
                return;
            }

            Undo.SetTransformParent(found, newParent, "Organize Stage-One Hierarchy");
            found.SetParent(newParent, true);
            EditorUtility.SetDirty(found.gameObject);
        }

        private static IEnumerable<Transform> GetSceneTransforms(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => GetChildrenRecursiveIncludingSelf(root.transform));
        }

        private static IEnumerable<Transform> GetChildrenRecursiveIncludingSelf(Transform parent)
        {
            yield return parent;

            foreach (Transform child in parent)
            {
                foreach (var descendant in GetChildrenRecursiveIncludingSelf(child))
                {
                    yield return descendant;
                }
            }
        }

        private static Transform FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            foreach (Transform child in root)
            {
                var found = FindByName(child, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool UsesSeparatorHierarchy(Transform root)
        {
            return root.Find(SeparatorName("Background")) != null ||
                   root.Find(SeparatorName("Environment")) != null ||
                   root.Find(SeparatorName("Puzzles")) != null;
        }

        private static void OrganizeSeparatorHierarchy(Scene scene, Transform root)
        {
            var background = EnsureSeparator(root, "Background");
            var environment = EnsureSeparator(root, "Environment");
            var ground = EnsureSeparator(root, "Ground");
            var playerGroup = EnsureSeparator(root, "Player");
            var puzzles = EnsureSeparator(root, "Puzzles");
            var collectibles = EnsureSeparator(root, "Collectibles");
            var systems = EnsureSeparator(root, "Systems");

            MoveIfExists(scene, "Main Camera", systems);
            MoveIfExists(scene, "StageOneManager", systems);
            MoveIfExists(scene, "Player", playerGroup);
            MoveIfExists(scene, "World Ground Pieces", ground);
            MoveIfExists(scene, "Respawn Points", systems);
            MoveIfExists(scene, "Memory Fragments", collectibles);

            var decorativeWorldArt = EnsureChild(environment, "Decorative World Art").transform;
            var front = EnsureChild(environment, "Front").transform;
            var storyProps = EnsureChild(environment, "Story Props").transform;
            var trees = EnsureChild(decorativeWorldArt, "Trees").transform;
            var rocks = EnsureChild(decorativeWorldArt, "Rocks").transform;

            var fog = MoveAndRenameIfExists(scene, background, "Fog Layers", "AllFoge", "Fog Layers");
            RenameSequentialChildren(fog, "FogLayer");

            var backgroundProps = MoveAndRenameIfExists(scene, background, "Background Props", "AllBG", "Background Props");
            RenameSequentialChildren(backgroundProps, "BackgroundProp");

            var foreground = MoveAndRenameIfExists(scene, front, "Foreground Overlays", "AllFront", "Foreground Overlays");
            RenameForegroundChildren(foreground);

            var grass = MoveAndRenameIfExists(scene, decorativeWorldArt, "Grass Details", "AllGrass", "Grass Details");
            RenameSequentialChildren(grass, "GrassDetail");

            MoveLooseTreeSprites(scene, trees);
            MoveDirectChildrenByPrefix(decorativeWorldArt, trees, "Tree-Silhouette", "TreeDetail");
            RenameSequentialChildren(trees, "TreeDetail");

            MoveDirectChildrenByPrefix(decorativeWorldArt, rocks, "Rock-");
            RenameSequentialChildren(rocks, "RockDetail");

            SortChildrenByName(background);
            SortChildrenByName(environment);
            SortChildrenByName(ground);
            SortChildrenByName(playerGroup);
            SortChildrenByName(puzzles);
            SortChildrenByName(collectibles);
            SortChildrenByName(systems);
            SetSiblingOrder(root,
                SeparatorName("Background"),
                SeparatorName("Environment"),
                SeparatorName("Ground"),
                SeparatorName("Player"),
                SeparatorName("Puzzles"),
                SeparatorName("Collectibles"),
                SeparatorName("Systems"));
            SetSiblingOrder(environment, "Back", "Play", "Front", "Decorative World Art", "Story Props");
            SetSiblingOrder(decorativeWorldArt, "Trees", "Grass Details", "Rocks");
            SetSiblingOrder(systems, "Main Camera", "StageOneManager", "Respawn Points");
        }

        private static Transform EnsureSeparator(Transform root, string label)
        {
            return EnsureChild(root, SeparatorName(label)).transform;
        }

        private static string SeparatorName(string label)
        {
            return "---------- " + label + " ----------";
        }

        private static Transform MoveAndRenameIfExists(Scene scene, Transform parent, string cleanName, params string[] possibleNames)
        {
            var found = possibleNames
                .Select(name => FindInScene(scene, name))
                .FirstOrDefault(transform => transform != null);

            if (found == null)
            {
                found = parent.Find(cleanName);
            }

            if (found == null)
            {
                return null;
            }

            MoveTransform(found, parent);
            RenameObject(found, cleanName);
            return found;
        }

        private static Transform FindInScene(Scene scene, string objectName)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindByName(root.transform, objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void MoveLooseTreeSprites(Scene scene, Transform trees)
        {
            var treeRoots = scene.GetRootGameObjects()
                .Select(root => root.transform)
                .Where(transform =>
                    transform.name == "tree" ||
                    transform.name.StartsWith("tree (", StringComparison.Ordinal) ||
                    transform.name.StartsWith("TreeType", StringComparison.Ordinal))
                .ToArray();

            foreach (var tree in treeRoots)
            {
                MoveTransform(tree, trees);
            }
        }

        private static void MoveDirectChildrenByPrefix(Transform source, Transform target, params string[] prefixes)
        {
            var children = source.Cast<Transform>()
                .Where(child => child != target)
                .Where(child => prefixes.Any(prefix => child.name.StartsWith(prefix, StringComparison.Ordinal)))
                .ToArray();

            foreach (var child in children)
            {
                MoveTransform(child, target);
            }
        }

        private static void RenameSequentialChildren(Transform parent, string prefix)
        {
            if (parent == null)
            {
                return;
            }

            var children = parent.Cast<Transform>()
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ThenBy(child => child.name, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < children.Length; i++)
            {
                children[i].SetSiblingIndex(i);
                RenameObject(children[i], prefix + "_" + (i + 1).ToString("000"));
            }
        }

        private static void RenameForegroundChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            var children = parent.Cast<Transform>()
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ThenBy(child => child.name, StringComparer.Ordinal)
                .ToArray();

            var layerIndex = 1;
            var maskIndex = 1;
            var propIndex = 1;

            foreach (var child in children)
            {
                child.SetSiblingIndex(System.Array.IndexOf(children, child));

                if (child.name.StartsWith("Rectangle", StringComparison.Ordinal))
                {
                    RenameObject(child, "ForegroundMask_" + maskIndex.ToString("000"));
                    maskIndex++;
                    continue;
                }

                if (child.name.StartsWith("Front", StringComparison.Ordinal))
                {
                    RenameObject(child, "ForegroundLayer_" + layerIndex.ToString("000"));
                    layerIndex++;
                    continue;
                }

                RenameObject(child, "ForegroundProp_" + propIndex.ToString("000"));
                propIndex++;
            }
        }

        private static void OrganizeEnvironment(Transform environment)
        {
            var oldBackground = environment.Find("BackGround");
            if (oldBackground != null)
            {
                oldBackground.name = "Background";
                EditorUtility.SetDirty(oldBackground.gameObject);
            }

            EnsureChild(environment, "Background");
            EnsureChild(environment, "Parallax");
            EnsureChild(environment, "Trees");
            EnsureChild(environment, "StageShell");
            EnsureChild(environment, "Floor");
            EnsureChild(environment, "FrontGround");
        }

        private static void OrganizeStageVisuals(Transform stageRoot, Transform environment)
        {
            var background = EnsureChild(environment, "Background").transform;
            var parallax = EnsureChild(environment, "Parallax").transform;
            var trees = EnsureChild(environment, "Trees").transform;
            var floor = EnsureChild(environment, "Floor").transform;
            var frontGround = EnsureChild(environment, "FrontGround").transform;

            var visualRoots = GetChildrenRecursive(stageRoot)
                .Where(child => child.GetComponent<ParallaxLayer2D>() != null)
                .Where(child => child.GetComponent<SpriteRenderer>() != null)
                .Where(child => !IsInsideGameplayOrPuzzle(child))
                .ToArray();

            foreach (var visual in visualRoots)
            {
                var targetParent = GetVisualParent(visual, background, parallax, trees, floor, frontGround);
                if (visual.parent != targetParent)
                {
                    Undo.SetTransformParent(visual, targetParent, "Organize Stage-One visuals");
                    visual.SetParent(targetParent, true);
                    EditorUtility.SetDirty(visual.gameObject);
                }
            }

            RenameVisualGroup(visualRoots, "BackGroundTheme", "BackgroundTheme");
            RenameVisualGroup(visualRoots, "DarkTop", "DarkVignetteOverlay");
            RenameVisualGroup(visualRoots, "Ground2", "GroundIsland");
            RenamePrefixGroup(visualRoots, "GroundIsland_", "GroundIsland");
            RenamePrefixGroup(visualRoots, "TreeType-", "TreeType");
            RenamePrefixGroup(visualRoots, "Wheel-Type-1-Background", "BackgroundWheelType1");
            RenamePrefixGroup(visualRoots, "Wheel-Type-2-Background", "BackgroundWheelType2");
            RenamePrefixGroup(visualRoots, "Wheel-Type-3-Background", "BackgroundWheelType3");
            RenameExactVisual(visualRoots, "Trees_Front", "ForegroundTrees");
            RenameExactVisual(visualRoots, "Islands-Background", "BackgroundIslands");
            RenameExactVisual(visualRoots, "OneIsland-Background", "BackgroundIsland");
            RenameFromSpriteName(visualRoots, "1");
            RenameFromSpriteName(visualRoots, "1_Background");
            OrganizeTreeTypeSprites(stageRoot, trees);

            SortChildrenByName(background);
            SortChildrenByName(parallax);
            SortChildrenByName(trees);
            SortChildrenByName(floor);
            SortChildrenByName(frontGround);
        }

        private static void OrganizeTreeTypeSprites(Transform stageRoot, Transform trees)
        {
            var treeSprites = GetChildrenRecursive(stageRoot)
                .Where(child => child.name.StartsWith("TreeType-", StringComparison.Ordinal))
                .Where(child => !IsInsideGameplayOrPuzzle(child))
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ToArray();

            for (var i = 0; i < treeSprites.Length; i++)
            {
                if (treeSprites[i].parent != trees)
                {
                    Undo.SetTransformParent(treeSprites[i], trees, "Organize Stage-One tree sprites");
                    treeSprites[i].SetParent(trees, true);
                }

                RenameObject(treeSprites[i], "TreeType_" + (i + 1).ToString("00"));
            }
        }

        private static bool IsInsideGameplayOrPuzzle(Transform transform)
        {
            for (var current = transform.parent; current != null; current = current.parent)
            {
                if (current.name == "Gameplay" || current.name == "Puzzles" || current.name == "Collectibles" || current.name == "Systems")
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform GetVisualParent(
            Transform visual,
            Transform background,
            Transform parallax,
            Transform trees,
            Transform floor,
            Transform frontGround)
        {
            var name = visual.name;
            var spriteName = GetSpriteName(visual);

            if (name.StartsWith("Ground2", StringComparison.Ordinal) || spriteName.Contains("GroundIsland", StringComparison.Ordinal))
            {
                return floor;
            }

            if (name.StartsWith("DarkTop", StringComparison.Ordinal) || spriteName.Contains("DarkVignette", StringComparison.Ordinal))
            {
                return frontGround;
            }

            if (name.StartsWith("TreeType", StringComparison.Ordinal) || spriteName.Contains("BareTree", StringComparison.Ordinal))
            {
                return trees;
            }

            if (name.StartsWith("BackGroundTheme", StringComparison.Ordinal) ||
                name.Contains("Background", StringComparison.Ordinal) ||
                name.Contains("BackGround", StringComparison.Ordinal) ||
                spriteName.Contains("_Back_", StringComparison.Ordinal))
            {
                return background;
            }

            return parallax;
        }

        private static string GetSpriteName(Transform transform)
        {
            var spriteRenderer = transform.GetComponent<SpriteRenderer>();
            return spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.name : string.Empty;
        }

        private static void RenameVisualGroup(IEnumerable<Transform> visuals, string currentPrefix, string cleanPrefix)
        {
            var matches = visuals
                .Where(visual => visual.name == currentPrefix || visual.name.StartsWith(currentPrefix + " (", StringComparison.Ordinal))
                .OrderBy(visual => visual.position.x)
                .ThenBy(visual => visual.position.y)
                .ToArray();

            for (var i = 0; i < matches.Length; i++)
            {
                RenameObject(matches[i], cleanPrefix + "_" + (i + 1).ToString("00"));
            }
        }

        private static void RenamePrefixGroup(IEnumerable<Transform> visuals, string currentPrefix, string cleanPrefix)
        {
            var matches = visuals
                .Where(visual => visual.name.StartsWith(currentPrefix, StringComparison.Ordinal))
                .OrderBy(visual => visual.position.x)
                .ThenBy(visual => visual.position.y)
                .ToArray();

            for (var i = 0; i < matches.Length; i++)
            {
                RenameObject(matches[i], cleanPrefix + "_" + (i + 1).ToString("00"));
            }
        }

        private static void RenameExactVisual(IEnumerable<Transform> visuals, string currentName, string cleanName)
        {
            foreach (var visual in visuals.Where(visual => visual.name == currentName).ToArray())
            {
                RenameObject(visual, cleanName);
            }
        }

        private static void RenameFromSpriteName(IEnumerable<Transform> visuals, string currentName)
        {
            foreach (var visual in visuals.Where(visual => visual.name == currentName).ToArray())
            {
                var spriteName = GetSpriteName(visual);
                if (string.IsNullOrEmpty(spriteName))
                {
                    continue;
                }

                var cleanName = spriteName.StartsWith("Parallax_", StringComparison.Ordinal)
                    ? spriteName
                    : "Parallax_" + spriteName;
                RenameObject(visual, cleanName);
            }
        }

        private static void RenameObject(Transform transform, string cleanName)
        {
            if (transform.name == cleanName)
            {
                return;
            }

            Undo.RecordObject(transform.gameObject, "Rename Stage-One hierarchy object");
            transform.name = cleanName;
            EditorUtility.SetDirty(transform.gameObject);
        }

        private static void OrganizePuzzleOne(Scene scene, Transform puzzles)
        {
            var puzzleOne = FindOrCreateSection(scene, puzzles, "PuzzleOne");

            var pieces = EnsureChild(puzzleOne, "Pieces").transform;
            var colliders = EnsureChild(puzzleOne, "Colliders").transform;
            var triggers = EnsureChild(puzzleOne, "Triggers").transform;

            MoveIfExists(scene, "PuzzleOneTrigger", triggers);
            MoveIfExists(scene, "TriggerZoomOut", triggers);
            MoveIfExists(scene, "TriggerZoomIn", triggers);
            MoveIfExists(scene, "CrumblingHandBridgeCollider", colliders);
            MoveIfExists(scene, "CrumblingHandRecoveredCollider", colliders);
            RemoveEmptyContainer(triggers.Find("PuzzleOneTrigger"));

            var handPieces = GetChildrenRecursive(puzzleOne)
                .Where(child => child.name.StartsWith("CrumblingHandPiece_", StringComparison.Ordinal))
                .Where(child => child.parent != pieces)
                .ToArray();

            foreach (var child in handPieces)
            {
                MoveTransform(child, pieces);
            }

            SortChildrenByName(pieces);
            SetSiblingOrder(puzzleOne, "Pieces", "Colliders", "Triggers");
            SetSiblingOrder(triggers, "TriggerZoomOut", "TriggerZoomIn");

            var bridge = puzzleOne.GetComponent<CrumblingHandBridgeCollapse>();
            if (bridge != null)
            {
                var serializedBridge = new SerializedObject(bridge);
                serializedBridge.FindProperty("pieceRoot").objectReferenceValue = pieces;
                serializedBridge.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(bridge);
            }
        }

        private static void OrganizePuzzleTwo(Scene scene, Transform puzzles)
        {
            var puzzleTwo = FindOrCreateSection(scene, puzzles, "PuzzleTwo");

            var wheel = EnsureChild(puzzleTwo, "Wheel").transform;
            var path = EnsureChild(puzzleTwo, "Path").transform;
            var triggers = EnsureChild(puzzleTwo, "Triggers").transform;

            MoveIfExists(scene, "SpinWheel", wheel);
            MoveIfExists(scene, "SpinWheelDestination", path);
            MoveIfExists(scene, "FallTrigger", triggers);

            var wheelWaypoints = GetSceneTransforms(scene)
                .Where(child => child.name.StartsWith("SpinWheelWaypoint_", StringComparison.Ordinal))
                .OrderBy(child => child.name, StringComparer.Ordinal)
                .ToArray();

            foreach (var waypoint in wheelWaypoints)
            {
                MoveTransform(waypoint, path);
            }

            SetSiblingOrder(puzzleTwo, "Wheel", "Path", "Triggers");
            SortChildrenByName(path);
            SortChildrenByName(triggers);
        }

        private static void OrganizePuzzleThree(Scene scene, Transform puzzles)
        {
            var puzzleThree = FindOrCreateSection(scene, puzzles, "PuzzleThree");
            var props = EnsureChild(puzzleThree, "Props").transform;
            var platforms = EnsureChild(puzzleThree, "Platforms").transform;
            var triggers = EnsureChild(puzzleThree, "Triggers").transform;

            MoveIfExists(scene, "BicycleBell_Small", props);

            SetSiblingOrder(puzzleThree, "Props", "Platforms", "Triggers");
            SortChildrenByName(props);
            SortChildrenByName(platforms);
            SortChildrenByName(triggers);
        }

        private static void OrganizePuzzleFour(Scene scene, Transform puzzles)
        {
            var puzzleFour = FindOrCreateSection(scene, puzzles, "PuzzleFour");
            var bridgeLift = EnsureChild(puzzleFour, "BridgeLift").transform;

            MoveIfExists(scene, "Up_Down_Islands", puzzleFour);
            MoveIfExists(scene, "UP_Down_island", puzzleFour);
            MoveIfExists(scene, "Up_Down_island", puzzleFour);
            MoveIfExists(scene, "Left_Right_Rotation", puzzleFour);
            MoveIfExists(scene, "Left_right_rotation", puzzleFour);
            MoveIfExists(scene, "StoneHandsBridge_Final", bridgeLift);
            MoveIfExists(scene, "BellRope", bridgeLift);

            var upDown = puzzleFour.Find("UP_Down_island") ?? puzzleFour.Find("Up_Down_island");
            if (upDown != null)
            {
                RenameObject(upDown, "Up_Down_Islands");
            }

            var leftRight = puzzleFour.Find("Left_right_rotation");
            if (leftRight != null)
            {
                RenameObject(leftRight, "Left_Right_Rotation");
            }

            SetSiblingOrder(puzzleFour, "Up_Down_Islands", "Left_Right_Rotation", "BridgeLift");
            SetSiblingOrder(bridgeLift, "StoneHandsBridge_Final", "BellRope");
        }

        private static void OrganizeGameplay(Scene scene, Transform gameplay)
        {
            var respawnPoints = EnsureChild(gameplay, "RespawnPoints").transform;
            MoveIfExists(scene, "Player", gameplay);

            var points = GetSceneTransforms(scene)
                .Where(child => child.name.StartsWith("RespawnPoint_", StringComparison.Ordinal))
                .ToArray();

            foreach (var point in points)
            {
                MoveTransform(point, respawnPoints);
            }

            var orderedPoints = respawnPoints.Cast<Transform>()
                .Where(child => child.name.StartsWith("RespawnPoint_", StringComparison.Ordinal))
                .OrderBy(child => child.position.x)
                .ThenBy(child => child.position.y)
                .ToArray();

            for (var i = 0; i < orderedPoints.Length; i++)
            {
                RenameObject(orderedPoints[i], "RespawnPoint_" + (i + 1).ToString("00"));
            }

            SortChildrenByName(respawnPoints);
        }

        private static void OrganizeCollectibles(Transform collectibles)
        {
            var memoryFragments = EnsureChild(collectibles, "MemoryFragments").transform;
            var directChildren = collectibles.Cast<Transform>().ToArray();

            foreach (var child in directChildren)
            {
                if (child == memoryFragments)
                {
                    continue;
                }

                if (!child.name.Contains("Fragment", StringComparison.Ordinal) &&
                    !child.name.Contains("Collectible", StringComparison.Ordinal))
                {
                    continue;
                }

                Undo.SetTransformParent(child, memoryFragments, "Organize collectibles");
                child.SetParent(memoryFragments, true);
            }

            SortChildrenByName(memoryFragments);
            SetSiblingOrder(collectibles, "MemoryFragments");
        }

        private static void RemoveEmptyContainer(Transform container)
        {
            if (container == null || container.childCount > 0)
            {
                return;
            }

            if (container.GetComponents<Component>().Length > 1)
            {
                return;
            }

            Undo.DestroyObjectImmediate(container.gameObject);
        }

        private static void SortChildrenByName(Transform parent)
        {
            var children = parent.Cast<Transform>()
                .OrderBy(child => child.name, StringComparer.Ordinal)
                .ToArray();

            for (var i = 0; i < children.Length; i++)
            {
                children[i].SetSiblingIndex(i);
            }
        }

        private static IEnumerable<Transform> GetChildrenRecursive(Transform parent)
        {
            foreach (Transform child in parent)
            {
                yield return child;

                foreach (var descendant in GetChildrenRecursive(child))
                {
                    yield return descendant;
                }
            }
        }

        private static void SetSiblingOrder(Transform parent, params string[] orderedNames)
        {
            var index = 0;
            foreach (var name in orderedNames)
            {
                var child = parent.Find(name);
                if (child == null)
                {
                    continue;
                }

                child.SetSiblingIndex(index);
                index++;
            }
        }

        private static string ProjectRelativeFilePath(string assetPath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }
    }
}
