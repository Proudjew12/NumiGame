using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class AutoSaveTime
    {
        private const string RootPath = "DreamScripts/AutoSaveTime";
        private const int RootMenuPriorityBase = 60050;

        private const string EnabledPrefKey = "DreamScripts.AutoSaveTime.Enabled";
        private const string MinutesPrefKey = "DreamScripts.AutoSaveTime.Minutes";
        private const string LegacyEnabledPrefKey = "DreamScripts." + "AutoSaveT" + "Ime.Enabled";
        private const string LegacyMinutesPrefKey = "DreamScripts." + "AutoSaveT" + "Ime.Minutes";
        private const int MinMinutes = 2;
        private const int MaxMinutes = 10;
        private const int DefaultMinutes = 5;

        private static bool s_enabled;
        private static int s_intervalMinutes;
        private static double s_nextSaveAt;

        static AutoSaveTime()
        {
            MigrateLegacyPrefs();
            s_enabled = EditorPrefs.GetBool(EnabledPrefKey, false);
            s_intervalMinutes = Mathf.Clamp(EditorPrefs.GetInt(MinutesPrefKey, DefaultMinutes), MinMinutes, MaxMinutes);
            ScheduleNextSave();
            EditorApplication.update += OnEditorUpdate;

            DreamScriptRegistry.Register("AutoSaveTime/Save Now", SaveNow, priority: 50, isEnabled: ValidateSaveNow);
            DreamScriptRegistry.Register("AutoSaveTime/Status", ShowStatus, priority: 51);
            DreamScriptRegistry.Register("AutoSaveTime/Off", TurnOff, priority: 52, isEnabled: ValidateTurnOff);
            DreamScriptRegistry.Register("AutoSaveTime/On/2 Minutes", SetOn2, priority: 53, isEnabled: ValidateOn2);
            DreamScriptRegistry.Register("AutoSaveTime/On/3 Minutes", SetOn3, priority: 54, isEnabled: ValidateOn3);
            DreamScriptRegistry.Register("AutoSaveTime/On/4 Minutes", SetOn4, priority: 55, isEnabled: ValidateOn4);
            DreamScriptRegistry.Register("AutoSaveTime/On/5 Minutes", SetOn5, priority: 56, isEnabled: ValidateOn5);
            DreamScriptRegistry.Register("AutoSaveTime/On/6 Minutes", SetOn6, priority: 57, isEnabled: ValidateOn6);
            DreamScriptRegistry.Register("AutoSaveTime/On/7 Minutes", SetOn7, priority: 58, isEnabled: ValidateOn7);
            DreamScriptRegistry.Register("AutoSaveTime/On/8 Minutes", SetOn8, priority: 59, isEnabled: ValidateOn8);
            DreamScriptRegistry.Register("AutoSaveTime/On/9 Minutes", SetOn9, priority: 60, isEnabled: ValidateOn9);
            DreamScriptRegistry.Register("AutoSaveTime/On/10 Minutes", SetOn10, priority: 61, isEnabled: ValidateOn10);
        }

        [MenuItem(RootPath + "/Save Now", false, RootMenuPriorityBase)]
        private static void SaveNow()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[AutoSaveTime] Save Now skipped while entering/in Play Mode.");
                return;
            }

            AutoSaveNow(manual: true);
            if (s_enabled)
            {
                ScheduleNextSave();
            }
        }

        [MenuItem(RootPath + "/Save Now", true, RootMenuPriorityBase)]
        private static bool ValidateSaveNow()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        [MenuItem(RootPath + "/Status", false, RootMenuPriorityBase + 1)]
        private static void ShowStatus()
        {
            Debug.Log("[AutoSaveTime] " + BuildStatusText());
        }

        [MenuItem(RootPath + "/Off", false, RootMenuPriorityBase + 5)]
        private static void TurnOff()
        {
            SetEnabled(false);
            Debug.Log("[AutoSaveTime] Turned OFF.");
        }

        [MenuItem(RootPath + "/Off", true, RootMenuPriorityBase + 5)]
        private static bool ValidateTurnOff()
        {
            Menu.SetChecked(RootPath + "/Off", !s_enabled);
            return true;
        }

        [MenuItem(RootPath + "/On/2 Minutes", false, RootMenuPriorityBase + 20)]
        private static void SetOn2() => SetOnMinutes(2);

        [MenuItem(RootPath + "/On/2 Minutes", true, RootMenuPriorityBase + 20)]
        private static bool ValidateOn2() => ValidateMinute(2);

        [MenuItem(RootPath + "/On/3 Minutes", false, RootMenuPriorityBase + 21)]
        private static void SetOn3() => SetOnMinutes(3);

        [MenuItem(RootPath + "/On/3 Minutes", true, RootMenuPriorityBase + 21)]
        private static bool ValidateOn3() => ValidateMinute(3);

        [MenuItem(RootPath + "/On/4 Minutes", false, RootMenuPriorityBase + 22)]
        private static void SetOn4() => SetOnMinutes(4);

        [MenuItem(RootPath + "/On/4 Minutes", true, RootMenuPriorityBase + 22)]
        private static bool ValidateOn4() => ValidateMinute(4);

        [MenuItem(RootPath + "/On/5 Minutes", false, RootMenuPriorityBase + 23)]
        private static void SetOn5() => SetOnMinutes(5);

        [MenuItem(RootPath + "/On/5 Minutes", true, RootMenuPriorityBase + 23)]
        private static bool ValidateOn5() => ValidateMinute(5);

        [MenuItem(RootPath + "/On/6 Minutes", false, RootMenuPriorityBase + 24)]
        private static void SetOn6() => SetOnMinutes(6);

        [MenuItem(RootPath + "/On/6 Minutes", true, RootMenuPriorityBase + 24)]
        private static bool ValidateOn6() => ValidateMinute(6);

        [MenuItem(RootPath + "/On/7 Minutes", false, RootMenuPriorityBase + 25)]
        private static void SetOn7() => SetOnMinutes(7);

        [MenuItem(RootPath + "/On/7 Minutes", true, RootMenuPriorityBase + 25)]
        private static bool ValidateOn7() => ValidateMinute(7);

        [MenuItem(RootPath + "/On/8 Minutes", false, RootMenuPriorityBase + 26)]
        private static void SetOn8() => SetOnMinutes(8);

        [MenuItem(RootPath + "/On/8 Minutes", true, RootMenuPriorityBase + 26)]
        private static bool ValidateOn8() => ValidateMinute(8);

        [MenuItem(RootPath + "/On/9 Minutes", false, RootMenuPriorityBase + 27)]
        private static void SetOn9() => SetOnMinutes(9);

        [MenuItem(RootPath + "/On/9 Minutes", true, RootMenuPriorityBase + 27)]
        private static bool ValidateOn9() => ValidateMinute(9);

        [MenuItem(RootPath + "/On/10 Minutes", false, RootMenuPriorityBase + 28)]
        private static void SetOn10() => SetOnMinutes(10);

        [MenuItem(RootPath + "/On/10 Minutes", true, RootMenuPriorityBase + 28)]
        private static bool ValidateOn10() => ValidateMinute(10);

        private static bool ValidateMinute(int minutes)
        {
            var isCurrent = s_enabled && s_intervalMinutes == minutes;
            var rootItem = RootPath + "/On/" + minutes + " Minutes";
            Menu.SetChecked(rootItem, isCurrent);
            return true;
        }

        private static void SetOnMinutes(int minutes)
        {
            s_intervalMinutes = Mathf.Clamp(minutes, MinMinutes, MaxMinutes);
            EditorPrefs.SetInt(MinutesPrefKey, s_intervalMinutes);
            SetEnabled(true);
            Debug.Log("[AutoSaveTime] ON every " + s_intervalMinutes + " minute(s).");
        }

        private static void SetEnabled(bool enabled)
        {
            s_enabled = enabled;
            EditorPrefs.SetBool(EnabledPrefKey, enabled);
            ScheduleNextSave();
        }

        private static void ScheduleNextSave()
        {
            s_nextSaveAt = EditorApplication.timeSinceStartup + (s_intervalMinutes * 60.0);
        }

        private static void OnEditorUpdate()
        {
            if (!s_enabled)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < s_nextSaveAt)
            {
                return;
            }

            AutoSaveNow(manual: false);
            ScheduleNextSave();
        }

        private static void AutoSaveNow(bool manual)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                var mode = manual ? "Manual Save Now" : "Auto-save";
                Debug.Log("[AutoSaveTime] " + mode + " skipped while entering/in Play Mode.");
                return;
            }

            var scenesSaved = EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            var completedMode = manual ? "Manual Save Now" : "Auto-save";
            Debug.Log("[AutoSaveTime] " + completedMode + " completed (scenesSaved=" + scenesSaved + ").");
        }

        private static void MigrateLegacyPrefs()
        {
            if (!EditorPrefs.HasKey(EnabledPrefKey) && EditorPrefs.HasKey(LegacyEnabledPrefKey))
            {
                EditorPrefs.SetBool(EnabledPrefKey, EditorPrefs.GetBool(LegacyEnabledPrefKey, false));
            }

            if (!EditorPrefs.HasKey(MinutesPrefKey) && EditorPrefs.HasKey(LegacyMinutesPrefKey))
            {
                EditorPrefs.SetInt(MinutesPrefKey, EditorPrefs.GetInt(LegacyMinutesPrefKey, DefaultMinutes));
            }
        }

        private static string BuildStatusText()
        {
            if (!s_enabled)
            {
                return "OFF";
            }

            var secondsLeft = Mathf.Max(0f, (float)(s_nextSaveAt - EditorApplication.timeSinceStartup));
            return "ON every " + s_intervalMinutes + " minute(s). Next save in ~" + Mathf.CeilToInt(secondsLeft) + "s.";
        }
    }
}
