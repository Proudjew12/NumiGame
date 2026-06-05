using NumiDream.DebugTools;
using UnityEditor;
using UnityEngine;

namespace DreamScripts.EditorTools
{
    [InitializeOnLoad]
    internal static class ControllerInputMonitorTools
    {
        private const string RootPath = "DreamScripts/Project/Controller Input Monitor";
        private const int RootMenuPriorityBase = 60055;

        static ControllerInputMonitorTools()
        {
            DreamScriptRegistry.Register("Project/Controller Input Monitor/Enable", Enable, priority: 55);
            DreamScriptRegistry.Register("Project/Controller Input Monitor/Disable", Disable, priority: 56);
            DreamScriptRegistry.Register("Project/Controller Input Monitor/Status", Status, priority: 57);
        }

        [MenuItem(RootPath + "/Enable", false, RootMenuPriorityBase)]
        private static void EnableFromMenu()
        {
            Enable();
        }

        [MenuItem(RootPath + "/Enable", true)]
        private static bool ValidateEnable()
        {
            Menu.SetChecked(RootPath + "/Enable", ControllerInputMonitor.IsAutoCreateEnabled);
            return true;
        }

        [MenuItem(RootPath + "/Disable", false, RootMenuPriorityBase + 1)]
        private static void DisableFromMenu()
        {
            Disable();
        }

        [MenuItem(RootPath + "/Disable", true)]
        private static bool ValidateDisable()
        {
            Menu.SetChecked(RootPath + "/Disable", !ControllerInputMonitor.IsAutoCreateEnabled);
            return true;
        }

        [MenuItem(RootPath + "/Status", false, RootMenuPriorityBase + 2)]
        private static void StatusFromMenu()
        {
            Status();
        }

        private static void Enable()
        {
            ControllerInputMonitor.SetAutoCreateEnabled(true);

            if (EditorApplication.isPlaying)
            {
                ControllerInputMonitor.EnsureExists(show: false);
            }

            Debug.Log("[ControllerInputMonitor] Enabled. Press F8 in Play Mode to show the overlay.");
        }

        private static void Disable()
        {
            ControllerInputMonitor.SetAutoCreateEnabled(false);
            RemoveLiveMonitor();
            Debug.Log("[ControllerInputMonitor] Disabled.");
        }

        private static void Status()
        {
            var status = ControllerInputMonitor.IsAutoCreateEnabled ? "enabled" : "disabled";
            EditorUtility.DisplayDialog(
                "Controller Input Monitor",
                "Controller Input Monitor is " + status + ".\n\n" +
                "When enabled, it listens in Play Mode. Press F8 to show or hide the overlay.\n\n" +
                "The overlay shows your physical 8BitDo button name beside the actual Unity control name.",
                "OK");
        }

        private static void RemoveLiveMonitor()
        {
            var monitor = Object.FindFirstObjectByType<ControllerInputMonitor>();
            if (monitor == null)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                Object.Destroy(monitor.gameObject);
            }
            else
            {
                Object.DestroyImmediate(monitor.gameObject);
            }
        }
    }
}
