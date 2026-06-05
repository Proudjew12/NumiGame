using System.Collections.Generic;
using System.Text;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NumiDream.DebugTools
{
    public sealed class ControllerInputMonitor : MonoBehaviour
    {
        public const string EnabledPlayerPrefsKey = "NumiDream.ControllerInputMonitor.Enabled";

        [SerializeField] private bool visible;
        [SerializeField] private bool showOnlyControllers = true;
        [SerializeField] private int maxRecentEvents = 14;
        [SerializeField] private float buttonThreshold = 0.5f;
        [SerializeField] private float axisThreshold = 0.35f;

        private readonly Queue<string> recentEvents = new Queue<string>();
        private readonly Dictionary<string, bool> activeControls = new Dictionary<string, bool>();
        private readonly StringBuilder textBuilder = new StringBuilder(2048);
        private readonly StringBuilder eventBuilder = new StringBuilder(512);

        private string lastEvent = "Press F8 to show controller input";
        private GUIStyle headerStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;

        public static bool IsAutoCreateEnabled => PlayerPrefs.GetInt(EnabledPlayerPrefsKey, 0) == 1;

        public static void SetAutoCreateEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(EnabledPlayerPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static ControllerInputMonitor EnsureExists(bool show = false)
        {
            var existing = FindFirstObjectByType<ControllerInputMonitor>();
            if (existing != null)
            {
                if (show)
                {
                    existing.visible = true;
                }

                return existing;
            }

            var monitorObject = new GameObject("Controller Input Monitor");
            DontDestroyOnLoad(monitorObject);
            var monitor = monitorObject.AddComponent<ControllerInputMonitor>();
            monitor.visible = show;
            return monitor;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoCreate()
        {
            if (IsAutoCreateEnabled)
            {
                EnsureExists(show: false);
            }
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.f8Key.wasPressedThisFrame)
                {
                    visible = !visible;
                }

                if (Keyboard.current.f9Key.wasPressedThisFrame)
                {
                    recentEvents.Clear();
                    lastEvent = "Cleared";
                }
            }

            foreach (var device in InputSystem.devices)
            {
                if (!ShouldWatchDevice(device))
                {
                    continue;
                }

                CaptureDeviceInput(device);
            }
#endif
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();

            var width = Mathf.Min(760f, Screen.width - 24f);
            var height = Mathf.Min(520f, Screen.height - 24f);
            var panelRect = new Rect(12f, 12f, width, height);

            var previousColor = GUI.color;
            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.88f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            GUILayout.BeginArea(new Rect(panelRect.x + 14f, panelRect.y + 12f, panelRect.width - 28f, panelRect.height - 24f));
            GUILayout.Label("Controller Input Monitor", headerStyle);
            GUILayout.Label(lastEvent, labelStyle);
            GUILayout.Space(8f);
            GUILayout.Label(BuildMonitorText(), smallStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (headerStyle != null)
            {
                return;
            }

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.72f, 0.9f, 1f) }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.92f, 0.96f) }
            };
        }

        private string BuildMonitorText()
        {
            textBuilder.Length = 0;

#if ENABLE_INPUT_SYSTEM
            textBuilder.AppendLine("Visible controller devices:");
            var foundDevice = false;
            foreach (var device in InputSystem.devices)
            {
                if (!ShouldWatchDevice(device))
                {
                    continue;
                }

                foundDevice = true;
                textBuilder.Append("- ");
                textBuilder.Append(device.displayName);
                textBuilder.Append(" | layout=");
                textBuilder.Append(device.layout);
                textBuilder.Append(" | name=");
                textBuilder.AppendLine(device.name);
            }

            if (!foundDevice)
            {
                textBuilder.AppendLine("- none");
            }

            textBuilder.AppendLine();
            textBuilder.AppendLine("Pressed buttons:");
            if (recentEvents.Count == 0)
            {
                textBuilder.AppendLine("- waiting");
            }
            else
            {
                foreach (var entry in recentEvents)
                {
                    textBuilder.AppendLine(entry);
                }
            }

            textBuilder.AppendLine();
            textBuilder.AppendLine("Keyboard: F8 show/hide, F9 clear");
#else
            textBuilder.AppendLine("Unity Input System is not enabled.");
#endif

            return textBuilder.ToString();
        }

#if ENABLE_INPUT_SYSTEM
        private bool ShouldWatchDevice(InputDevice device)
        {
            if (!showOnlyControllers)
            {
                return true;
            }

            if (device is Gamepad || device is Joystick)
            {
                return true;
            }

            return ContainsControllerName(device.displayName)
                || ContainsControllerName(device.name)
                || ContainsControllerName(device.layout)
                || ContainsControllerName(device.description.product)
                || ContainsControllerName(device.description.manufacturer);
        }

        private static bool ContainsControllerName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.IndexOf("8BitDo", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Controller", System.StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Joystick", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CaptureDeviceInput(InputDevice device)
        {
            foreach (var control in device.allControls)
            {
                if (control.synthetic && control.parent is ButtonControl)
                {
                    continue;
                }

                if (control is ButtonControl button)
                {
                    CaptureButton(device, button);
                    continue;
                }

                if (control is Vector2Control vector)
                {
                    CaptureVector(device, vector);
                    continue;
                }

                if (control is AxisControl axis)
                {
                    CaptureAxis(device, axis);
                }
            }
        }

        private void CaptureButton(InputDevice device, ButtonControl button)
        {
            var key = button.path;
            var isActive = button.ReadValue() >= buttonThreshold;
            activeControls.TryGetValue(key, out var wasActive);
            activeControls[key] = isActive;

            if (isActive && (!wasActive || button.wasPressedThisFrame))
            {
                AddEvent(device, button, button.ReadValue().ToString("0.00"));
            }
        }

        private void CaptureAxis(InputDevice device, AxisControl axis)
        {
            if (axis.parent is StickControl || axis.parent is DpadControl)
            {
                return;
            }

            var value = axis.ReadValue();
            var key = axis.path;
            var isActive = Mathf.Abs(value) >= axisThreshold;
            activeControls.TryGetValue(key, out var wasActive);
            activeControls[key] = isActive;

            if (isActive && !wasActive)
            {
                AddEvent(device, axis, value.ToString("0.00"));
            }
        }

        private void CaptureVector(InputDevice device, Vector2Control vector)
        {
            var value = vector.ReadValue();
            var key = vector.path;
            var isActive = value.magnitude >= axisThreshold;
            activeControls.TryGetValue(key, out var wasActive);
            activeControls[key] = isActive;

            if (isActive && !wasActive)
            {
                AddEvent(device, vector, value.ToString("0.00"));
            }
        }

        private void AddEvent(InputDevice device, InputControl control, string value)
        {
            eventBuilder.Length = 0;
            eventBuilder.Append("U Have Pressed button ");
            eventBuilder.Append(GetControllerButtonName(control));
            eventBuilder.Append(" | Unity gives: ");
            eventBuilder.Append(control.name);
            eventBuilder.Append(" | path=");
            eventBuilder.Append(control.path);
            eventBuilder.Append(" | value=");
            eventBuilder.Append(value);

            var line = eventBuilder.ToString();

            lastEvent = line;
            recentEvents.Enqueue("- " + line);

            while (recentEvents.Count > Mathf.Max(1, maxRecentEvents))
            {
                recentEvents.Dequeue();
            }
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string GetControllerButtonName(InputControl control)
        {
            switch (control.name)
            {
                case "Y":
                    return "A";
                case "TriggerRight":
                    return "X";
                case "TriggerLeft2":
                    return "Y";
                case "Start":
                    return "RB";
                case "Select":
                    return "LB";
                case "RotateZ":
                    return "RT";
                case "Z":
                    return "LT";
                default:
                    return SafeText(control.displayName);
            }
        }
#endif
    }
}
