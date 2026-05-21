using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class RefreshFallback
    {
        private const string RootPath = "DreamScripts/Refresh Fallback";
        private const int RootMenuPriority = 60045;
        private const string EnabledPrefKey = "DreamScripts.RefreshFallback.Enabled";
        // Only refresh when Unity regains focus after being in another app for a while.
        // This avoids hierarchy/UI reset side effects on quick in-editor actions.
        private const double MinFocusLossSeconds = 8.0;

        private static bool s_enabled;
        private static double s_nextAllowedRefreshTime;
        private static double s_lastFocusLostTime = -1d;
        private static bool s_focusRefreshQueued;

        static RefreshFallback()
        {
            s_enabled = EditorPrefs.GetBool(EnabledPrefKey, true);
            DreamScriptRegistry.Register("Refresh Fallback/Enable", Enable, priority: 45);
            DreamScriptRegistry.Register("Refresh Fallback/Disable", Disable, priority: 46);
            DreamScriptRegistry.Register("Refresh Fallback/Refresh Now", RefreshNow, priority: 47);
            EditorApplication.delayCall += RefreshOnStartup;
            EditorApplication.focusChanged += OnFocusChanged;
        }

        [MenuItem(RootPath + "/Enable", false, RootMenuPriority)]
        private static void Enable()
        {
            s_enabled = true;
            EditorPrefs.SetBool(EnabledPrefKey, true);
            Debug.Log("[RefreshFallback] Enabled.");
        }

        [MenuItem(RootPath + "/Enable", true, RootMenuPriority)]
        private static bool ValidateEnable()
        {
            Menu.SetChecked(RootPath + "/Enable", s_enabled);
            return true;
        }

        [MenuItem(RootPath + "/Disable", false, RootMenuPriority + 1)]
        private static void Disable()
        {
            s_enabled = false;
            EditorPrefs.SetBool(EnabledPrefKey, false);
            Debug.Log("[RefreshFallback] Disabled.");
        }

        [MenuItem(RootPath + "/Disable", true, RootMenuPriority + 1)]
        private static bool ValidateDisable()
        {
            return true;
        }

        [MenuItem(RootPath + "/Refresh Now", false, RootMenuPriority + 2)]
        private static void RefreshNow()
        {
            TryRefresh("manual");
        }

        private static void RefreshOnStartup()
        {
            TryRefresh("startup");
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (!s_enabled)
            {
                return;
            }

            if (!hasFocus)
            {
                s_lastFocusLostTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (s_lastFocusLostTime < 0d)
            {
                return;
            }

            var secondsOutOfEditor = EditorApplication.timeSinceStartup - s_lastFocusLostTime;
            if (secondsOutOfEditor < MinFocusLossSeconds)
            {
                return;
            }

            if (s_focusRefreshQueued)
            {
                return;
            }

            if (IsHierarchyWindowActive())
            {
                return;
            }

            s_focusRefreshQueued = true;
            EditorApplication.delayCall += RefreshAfterFocusDelay;
        }

        private static void RefreshAfterFocusDelay()
        {
            s_focusRefreshQueued = false;
            TryRefresh("focus");
        }

        private static void TryRefresh(string source)
        {
            if (!s_enabled)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < s_nextAllowedRefreshTime)
            {
                return;
            }

            s_nextAllowedRefreshTime = now + 1.5;
            AssetDatabase.Refresh();

            if (IsAutomaticSource(source))
            {
                ClearConsole();
                return;
            }

            Debug.Log("[RefreshFallback] AssetDatabase.Refresh triggered (" + source + ").");
        }

        private static bool IsAutomaticSource(string source)
        {
            return source == "startup" || source == "focus";
        }

        private static bool IsHierarchyWindowActive()
        {
            var focused = EditorWindow.focusedWindow;
            if (focused == null)
            {
                return false;
            }

            return focused.GetType().Name == "SceneHierarchyWindow";
        }

        private static void ClearConsole()
        {
            var logEntriesType = typeof(Editor).Assembly.GetType("UnityEditor.LogEntries");
            var clearMethod = logEntriesType?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static);
            clearMethod?.Invoke(null, null);
        }
    }
}
