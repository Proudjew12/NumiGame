using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
