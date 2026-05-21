using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NumiDream.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true)]
    internal sealed class NumiDreamTooltipInspector : UnityEditor.Editor
    {
        private const string RuntimeNamespacePrefix = "NumiDream.";
        private const string ScriptPropertyPath = "m_Script";

        public override void OnInspectorGUI()
        {
            if (!ShouldDrawNumiDreamInspector())
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            var property = serializedObject.GetIterator();
            var enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                using (new EditorGUI.DisabledScope(property.propertyPath == ScriptPropertyPath))
                {
                    EditorGUILayout.PropertyField(property, CreateLabel(property), includeChildren: true);
                }

                enterChildren = false;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool ShouldDrawNumiDreamInspector()
        {
            for (var i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    continue;
                }

                var typeNamespace = targets[i].GetType().Namespace;
                if (typeNamespace == null ||
                    !typeNamespace.StartsWith(RuntimeNamespacePrefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static GUIContent CreateLabel(SerializedProperty property)
        {
            if (property.propertyPath == ScriptPropertyPath)
            {
                return new GUIContent(property.displayName);
            }

            var fullFieldName = GetFullFieldName(property);
            return new GUIContent(GetVisibleFieldName(property, fullFieldName), fullFieldName);
        }

        private static string GetFullFieldName(SerializedProperty property)
        {
            var fieldName = property.name;
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return property.displayName;
            }

            return ObjectNames.NicifyVariableName(fieldName);
        }

        private static string GetVisibleFieldName(SerializedProperty property, string fallbackName)
        {
            var field = FindField(property);
            var inspectorName = field?.GetCustomAttribute<InspectorNameAttribute>();
            if (inspectorName != null && !string.IsNullOrWhiteSpace(inspectorName.displayName))
            {
                return inspectorName.displayName;
            }

            return fallbackName;
        }

        private static FieldInfo FindField(SerializedProperty property)
        {
            var target = property.serializedObject.targetObject;
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var fieldName = property.propertyPath.Split('.')[0];
            while (type != null)
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
