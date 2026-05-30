using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class CopyComponentTool
    {
        private const int RootMenuPriorityBase = 60085;
        private const string ClipboardKey = "DreamScripts.CopyComponent.Clipboard";
        private const string ClipboardListKey = "DreamScripts.CopyComponent.ClipboardList";
        private const string LegacyClipboardKey = "DreamScripts." + "CopyPlay" + "Component.Clipboard";
        private const string LegacyClipboardListKey = "DreamScripts." + "CopyPlay" + "Component.ClipboardList";
        private const string MenuCopy = "DreamScripts/CopyComponent/Copy";
        private const string MenuPaste = "DreamScripts/CopyComponent/Paste";
        private const string MenuCopyAll = "DreamScripts/CopyComponent/Copy All From Selected GameObject";
        private const string MenuPasteAll = "DreamScripts/CopyComponent/Paste All To Selected GameObject";
        private const string MenuRightClickPlayerOn = "DreamScripts/RightClickPlayer/On";
        private const string MenuRightClickPlayerOff = "DreamScripts/RightClickPlayer/Off";
        private const string RightClickPlayerEnabledKey = "DreamScripts.CopyComponent.RightClickPlayer.Enabled";
        private static bool s_gameRightMouseWasDown;

        [Serializable]
        private struct ClipboardData
        {
            public string componentType;
            public string json;
            public string sourcePath;
            public string copiedAtUtc;
        }

        [Serializable]
        private struct ClipboardDataList
        {
            public ClipboardData[] components;
            public string sourcePath;
            public string copiedAtUtc;
        }

        static CopyComponentTool()
        {
            DreamScriptRegistry.Register("CopyComponent/Copy", CopyFromMenu, priority: 90, isEnabled: CanCopy);
            DreamScriptRegistry.Register("CopyComponent/Paste", PasteFromMenu, priority: 91, isEnabled: CanPaste);
            DreamScriptRegistry.Register("CopyComponent/Copy All", CopyAllFromMenu, priority: 92, isEnabled: CanCopyAll);
            DreamScriptRegistry.Register("CopyComponent/Paste All", PasteAllFromMenu, priority: 93, isEnabled: CanPasteAll);
            DreamScriptRegistry.Register("RightClickPlayer/On", TurnRightClickPlayerOn, priority: 94, isEnabled: () => !IsRightClickPlayerEnabled);
            DreamScriptRegistry.Register("RightClickPlayer/Off", TurnRightClickPlayerOff, priority: 95, isEnabled: () => IsRightClickPlayerEnabled);

            RefreshRightClickPlayerHooks();
        }

        [MenuItem(MenuCopy, false, RootMenuPriorityBase)]
        private static void CopyFromMenu()
        {
            if (Selection.activeObject is Component selectedComponent)
            {
                CopyComponentData(selectedComponent);
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowInfo("Select a GameObject or component first.");
                return;
            }

            ShowCopyPicker(go.GetComponents<Component>());
        }

        [MenuItem(MenuCopy, true, RootMenuPriorityBase)]
        private static bool ValidateCopyFromMenu()
        {
            return CanCopy();
        }

        [MenuItem(MenuPaste, false, RootMenuPriorityBase + 1)]
        private static void PasteFromMenu()
        {
            if (!TryReadClipboard(out var data))
            {
                ShowInfo("Nothing copied yet. Use CopyComponent/Copy in Play Mode first.");
                return;
            }

            var componentType = Type.GetType(data.componentType);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
            {
                ShowInfo("Copied data is invalid or from an unavailable component type.");
                return;
            }

            if (Selection.activeObject is Component selectedComponent)
            {
                if (!componentType.IsInstanceOfType(selectedComponent))
                {
                    ShowInfo("Selected component type does not match copied data.");
                    return;
                }

                PasteIntoComponent(selectedComponent, data);
                return;
            }

            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowInfo("Select the target GameObject or target component first.");
                return;
            }

            var matches = go.GetComponents(componentType);
            if (matches == null || matches.Length == 0)
            {
                ShowInfo("Selected GameObject has no matching component to paste into.");
                return;
            }

            if (matches.Length == 1)
            {
                PasteIntoComponent(matches[0], data);
                return;
            }

            ShowPastePicker(matches, data);
        }

        [MenuItem(MenuPaste, true, RootMenuPriorityBase + 1)]
        private static bool ValidatePasteFromMenu()
        {
            return CanPaste();
        }

        [MenuItem(MenuCopyAll, false, RootMenuPriorityBase + 2)]
        private static void CopyAllFromMenu()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowInfo("Select a GameObject first.");
                return;
            }

            CopyAllComponents(go);
        }

        [MenuItem(MenuCopyAll, true, RootMenuPriorityBase + 2)]
        private static bool ValidateCopyAllFromMenu()
        {
            return CanCopyAll();
        }

        [MenuItem(MenuPasteAll, false, RootMenuPriorityBase + 3)]
        private static void PasteAllFromMenu()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                ShowInfo("Select the target GameObject first.");
                return;
            }

            PasteAllComponents(go);
        }

        [MenuItem(MenuPasteAll, true, RootMenuPriorityBase + 3)]
        private static bool ValidatePasteAllFromMenu()
        {
            return CanPasteAll();
        }

        [MenuItem(MenuRightClickPlayerOn, false, RootMenuPriorityBase + 4)]
        private static void TurnRightClickPlayerOn()
        {
            IsRightClickPlayerEnabled = true;
            RefreshRightClickPlayerHooks();
            ShowInfo("RightClickPlayer is On. Right-click in Scene view, or in Game view during Play Mode, to move Player.");
        }

        [MenuItem(MenuRightClickPlayerOn, true, RootMenuPriorityBase + 4)]
        private static bool ValidateTurnRightClickPlayerOn()
        {
            return !IsRightClickPlayerEnabled;
        }

        [MenuItem(MenuRightClickPlayerOff, false, RootMenuPriorityBase + 5)]
        private static void TurnRightClickPlayerOff()
        {
            IsRightClickPlayerEnabled = false;
            RefreshRightClickPlayerHooks();
            ShowInfo("RightClickPlayer is Off.");
        }

        [MenuItem(MenuRightClickPlayerOff, true, RootMenuPriorityBase + 5)]
        private static bool ValidateTurnRightClickPlayerOff()
        {
            return IsRightClickPlayerEnabled;
        }

        [MenuItem("CONTEXT/Component/CopyComponent/Copy")]
        private static void CopyFromContext(MenuCommand command)
        {
            var component = command.context as Component;
            if (component == null)
            {
                ShowInfo("No component context found.");
                return;
            }

            CopyComponentData(component);
        }

        [MenuItem("CONTEXT/Component/CopyComponent/Paste")]
        private static void PasteFromContext(MenuCommand command)
        {
            var component = command.context as Component;
            if (component == null)
            {
                ShowInfo("No component context found.");
                return;
            }

            if (!TryReadClipboard(out var data))
            {
                ShowInfo("Nothing copied yet. Use Copy first.");
                return;
            }

            var componentType = Type.GetType(data.componentType);
            if (componentType == null || !componentType.IsInstanceOfType(component))
            {
                ShowInfo("Copied data type does not match this component.");
                return;
            }

            PasteIntoComponent(component, data);
        }

        private static bool CanCopy()
        {
            return Selection.activeObject is Component || Selection.activeGameObject != null;
        }

        private static bool CanPaste()
        {
            return TryReadClipboard(out _) && (Selection.activeObject is Component || Selection.activeGameObject != null);
        }

        private static bool CanCopyAll()
        {
            return Selection.activeGameObject != null;
        }

        private static bool CanPasteAll()
        {
            return TryReadClipboardList(out _) && Selection.activeGameObject != null;
        }

        private static bool IsRightClickPlayerEnabled
        {
            get => EditorPrefs.GetBool(RightClickPlayerEnabledKey, false);
            set => EditorPrefs.SetBool(RightClickPlayerEnabledKey, value);
        }

        private static void RefreshRightClickPlayerHooks()
        {
            SceneView.duringSceneGui -= HandleRightClickPlayerSceneGui;
            EditorApplication.update -= HandleRightClickPlayerGameViewUpdate;
            s_gameRightMouseWasDown = false;

            if (!IsRightClickPlayerEnabled)
            {
                return;
            }

            SceneView.duringSceneGui += HandleRightClickPlayerSceneGui;
            EditorApplication.update += HandleRightClickPlayerGameViewUpdate;
        }

        private static void HandleRightClickPlayerSceneGui(SceneView sceneView)
        {
            if (!IsRightClickPlayerEnabled || sceneView == null)
            {
                return;
            }

            var evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 1 || evt.alt)
            {
                return;
            }

            var player = FindPlayerTransform();
            if (player == null)
            {
                ShowInfo("RightClickPlayer could not find a Player object.");
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            if (!TryIntersectPlayerPlane(ray, player.position.z, out var targetPosition))
            {
                return;
            }

            MovePlayerTo(player, targetPosition, "Scene view");
            evt.Use();
        }

        private static void HandleRightClickPlayerGameViewUpdate()
        {
            if (!IsRightClickPlayerEnabled || !Application.isPlaying || IsMouseOverSceneView() || !IsGameViewInteractionActive())
            {
                s_gameRightMouseWasDown = false;
                return;
            }

            var rightMouseDown = IsRightMouseDownInGame();
            var rightMousePressed = rightMouseDown && !s_gameRightMouseWasDown;
            s_gameRightMouseWasDown = rightMouseDown;

            if (!rightMousePressed)
            {
                return;
            }

            var player = FindPlayerTransform();
            if (player == null)
            {
                ShowInfo("RightClickPlayer could not find a Player object.");
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                ShowInfo("RightClickPlayer could not find a camera for Game view.");
                return;
            }

            var ray = camera.ScreenPointToRay(GetGameMousePosition());
            if (!TryIntersectPlayerPlane(ray, player.position.z, out var targetPosition))
            {
                return;
            }

            MovePlayerTo(player, targetPosition, "Game view");
        }

        private static Transform FindPlayerTransform()
        {
            GameObject playerObject = null;

            try
            {
                playerObject = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                playerObject = null;
            }

            if (playerObject == null)
            {
                playerObject = GameObject.Find("Player");
            }

            if (playerObject == null)
            {
                return null;
            }

            var body = playerObject.GetComponent<Rigidbody2D>() ?? playerObject.GetComponentInParent<Rigidbody2D>();
            return body != null ? body.transform : playerObject.transform;
        }

        private static bool TryIntersectPlayerPlane(Ray ray, float playerZ, out Vector3 position)
        {
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, playerZ));
            if (plane.Raycast(ray, out var distance))
            {
                position = ray.GetPoint(distance);
                position.z = playerZ;
                return true;
            }

            position = default;
            return false;
        }

        private static void MovePlayerTo(Transform player, Vector3 targetPosition, string source)
        {
            if (player == null)
            {
                return;
            }

            targetPosition.z = player.position.z;
            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Right Click Move Player");

            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = new Vector2(targetPosition.x, targetPosition.y);
            }

            player.position = targetPosition;
            Physics2D.SyncTransforms();
            EditorUtility.SetDirty(player);

            if (player.gameObject.scene.IsValid() && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            }

            ShowInfo("Moved Player to " + FormatPosition(targetPosition) + " from " + source + ".");
        }

        private static bool IsGameViewInteractionActive()
        {
            if (IsGameViewWindow(EditorWindow.focusedWindow) || IsGameViewWindow(EditorWindow.mouseOverWindow))
            {
                return true;
            }

            return Application.isFocused;
        }

        private static bool IsMouseOverSceneView()
        {
            return EditorWindow.mouseOverWindow is SceneView;
        }

        private static bool IsGameViewWindow(EditorWindow window)
        {
            return window != null && window.GetType().Name.IndexOf("GameView", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRightMouseDownInGame()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                return Mouse.current.rightButton.isPressed;
            }
#endif

            try
            {
                return Input.GetMouseButton(1);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static Vector3 GetGameMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var position = Mouse.current.position.ReadValue();
                return new Vector3(position.x, position.y, 0f);
            }
#endif

            try
            {
                return Input.mousePosition;
            }
            catch (InvalidOperationException)
            {
                return Vector3.zero;
            }
        }

        private static string FormatPosition(Vector3 position)
        {
            return "(" + position.x.ToString("0.##") + ", " + position.y.ToString("0.##") + ", " + position.z.ToString("0.##") + ")";
        }

        private static void ShowCopyPicker(Component[] components)
        {
            if (components == null || components.Length == 0)
            {
                ShowInfo("Selected GameObject has no components.");
                return;
            }

            var menu = new GenericMenu();
            var hasItem = false;

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                hasItem = true;
                var componentLabel = BuildComponentLabel(component, i);
                menu.AddItem(new GUIContent(componentLabel), false, () => CopyComponentData(component));
            }

            if (!hasItem)
            {
                ShowInfo("Selected GameObject has no valid components.");
                return;
            }

            menu.ShowAsContext();
        }

        private static void ShowPastePicker(Component[] matches, ClipboardData data)
        {
            var menu = new GenericMenu();

            for (var i = 0; i < matches.Length; i++)
            {
                var component = matches[i];
                if (component == null)
                {
                    continue;
                }

                var componentLabel = BuildComponentLabel(component, i);
                menu.AddItem(new GUIContent(componentLabel), false, () => PasteIntoComponent(component, data));
            }

            menu.ShowAsContext();
        }

        private static string BuildComponentLabel(Component component, int index)
        {
            var typeName = component.GetType().Name;
            return index + ": " + typeName;
        }

        private static void CopyComponentData(Component source)
        {
            if (source == null)
            {
                ShowInfo("No component selected.");
                return;
            }

            var data = new ClipboardData
            {
                componentType = source.GetType().AssemblyQualifiedName,
                json = EditorJsonUtility.ToJson(source, true),
                sourcePath = BuildPath(source.transform),
                copiedAtUtc = DateTime.UtcNow.ToString("o")
            };

            var payload = JsonUtility.ToJson(data);
            SessionState.SetString(ClipboardKey, payload);
            EditorPrefs.SetString(ClipboardKey, payload);
            ShowInfo("Copied " + source.GetType().Name + " from " + data.sourcePath + ".");
        }

        private static void PasteIntoComponent(Component target, ClipboardData data)
        {
            if (target == null)
            {
                ShowInfo("No target component selected.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(target, "Paste Play Component");
            EditorJsonUtility.FromJsonOverwrite(data.json, target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);

            if (target.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            }

            ShowInfo("Pasted values into " + target.GetType().Name + " on " + BuildPath(target.transform) + ".");
        }

        private static void CopyAllComponents(GameObject source)
        {
            var components = source.GetComponents<Component>();
            var data = new ClipboardData[components.Length];
            var count = 0;

            foreach (var component in components)
            {
                if (component == null)
                {
                    continue;
                }

                data[count] = new ClipboardData
                {
                    componentType = component.GetType().AssemblyQualifiedName,
                    json = EditorJsonUtility.ToJson(component, true),
                    sourcePath = BuildPath(component.transform),
                    copiedAtUtc = DateTime.UtcNow.ToString("o")
                };
                count++;
            }

            Array.Resize(ref data, count);

            if (data.Length == 0)
            {
                ShowInfo("Selected GameObject has no valid components.");
                return;
            }

            var list = new ClipboardDataList
            {
                components = data,
                sourcePath = BuildPath(source.transform),
                copiedAtUtc = DateTime.UtcNow.ToString("o")
            };

            var payload = JsonUtility.ToJson(list);
            SessionState.SetString(ClipboardListKey, payload);
            EditorPrefs.SetString(ClipboardListKey, payload);
            ShowInfo("Copied " + data.Length + " components from " + list.sourcePath + ".");
        }

        private static void PasteAllComponents(GameObject target)
        {
            if (!TryReadClipboardList(out var list))
            {
                ShowInfo("Nothing copied yet. Use Copy All in Play Mode first.");
                return;
            }

            var pasted = 0;
            Undo.RegisterFullObjectHierarchyUndo(target, "Paste Play Components");

            foreach (var data in list.components)
            {
                var componentType = Type.GetType(data.componentType);
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                {
                    continue;
                }

                var targetComponent = target.GetComponent(componentType);
                if (targetComponent == null)
                {
                    continue;
                }

                EditorJsonUtility.FromJsonOverwrite(data.json, targetComponent);
                PrefabUtility.RecordPrefabInstancePropertyModifications(targetComponent);
                EditorUtility.SetDirty(targetComponent);
                pasted++;
            }

            if (target.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(target.scene);
            }

            ShowInfo(pasted == 0
                ? "No matching components found on " + BuildPath(target.transform) + "."
                : "Pasted " + pasted + " components into " + BuildPath(target.transform) + ".");
        }

        private static bool TryReadClipboard(out ClipboardData data)
        {
            data = default;

            var payload = SessionState.GetString(ClipboardKey, string.Empty);
            if (string.IsNullOrEmpty(payload))
            {
                payload = EditorPrefs.GetString(ClipboardKey, string.Empty);
                if (string.IsNullOrEmpty(payload))
                {
                    payload = EditorPrefs.GetString(LegacyClipboardKey, string.Empty);
                }

                if (string.IsNullOrEmpty(payload))
                {
                    return false;
                }
            }

            data = JsonUtility.FromJson<ClipboardData>(payload);
            return !string.IsNullOrEmpty(data.componentType) && !string.IsNullOrEmpty(data.json);
        }

        private static bool TryReadClipboardList(out ClipboardDataList data)
        {
            data = default;

            var payload = SessionState.GetString(ClipboardListKey, string.Empty);
            if (string.IsNullOrEmpty(payload))
            {
                payload = EditorPrefs.GetString(ClipboardListKey, string.Empty);
                if (string.IsNullOrEmpty(payload))
                {
                    payload = EditorPrefs.GetString(LegacyClipboardListKey, string.Empty);
                }

                if (string.IsNullOrEmpty(payload))
                {
                    return false;
                }
            }

            data = JsonUtility.FromJson<ClipboardDataList>(payload);
            return data.components != null && data.components.Length > 0;
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return "(unknown)";
            }

            var path = transform.name;
            var current = transform.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void ShowInfo(string message)
        {
            Debug.Log("[CopyComponent] " + message);
            var focused = EditorWindow.focusedWindow;
            focused?.ShowNotification(new GUIContent(message));
        }
    }
}
