using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NumiDream.Tests.EditMode
{
    public sealed class ControllerInputTests
    {
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void PlayerInputActionsContain8BitDoGamepadAndJoystickBindings()
        {
            var fullPath = Path.Combine(Application.dataPath, "..", InputActionsPath);
            var actionsJson = File.ReadAllText(fullPath);

            StringAssert.Contains("\"path\": \"<Gamepad>/dpad\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/stick\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/{Hatswitch}\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/dpad/up\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/trigger\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/buttonNorth\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/rightShoulder\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/rightTrigger\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/leftShoulder\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Gamepad>/leftTrigger\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/Y\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/TriggerRight\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/TriggerLeft2\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/Start\"", actionsJson);
            StringAssert.Contains("\"path\": \"<Joystick>/Select\"", actionsJson);
        }

        [Test]
        public void EightBitDoAnalogTriggersAreHandledInCodeNotRawInputActions()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var actionsJson = File.ReadAllText(Path.Combine(projectRoot, InputActionsPath));
            var inputHelper = File.ReadAllText(Path.Combine(projectRoot, "Assets/NumiDream/Scripts/NumiInput.cs"));
            var monitor = File.ReadAllText(Path.Combine(projectRoot, "Assets/NumiDream/Scripts/Debug/ControllerInputMonitor.cs"));

            Assert.That(actionsJson, Does.Not.Contain("\"path\": \"<Joystick>/RotateZ\""));
            Assert.That(actionsJson, Does.Not.Contain("\"path\": \"<Joystick>/Z\""));
            Assert.That(inputHelper, Does.Contain("\"RotateZ\""));
            Assert.That(inputHelper, Does.Contain("\"Z\""));
            Assert.That(inputHelper, Does.Contain("WasFilteredAxisPressed"));
            Assert.That(monitor, Does.Not.Contain("axisRepeatDelay"));
        }

        [Test]
        public void ControllerInputMonitorStartsHiddenAndUsesReadableButtonText()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            var monitor = File.ReadAllText(Path.Combine(projectRoot, "Assets/NumiDream/Scripts/Debug/ControllerInputMonitor.cs"));
            var tools = File.ReadAllText(Path.Combine(projectRoot, "Assets/DreamScripts/Editor/ControllerInputMonitorTools.cs"));

            Assert.That(monitor, Does.Contain("private bool visible;"));
            Assert.That(monitor, Does.Contain("Press F8 to show controller input"));
            Assert.That(monitor, Does.Contain("EnsureExists(show: false)"));
            Assert.That(monitor, Does.Contain("U Have Pressed button "));
            Assert.That(monitor, Does.Contain("Unity gives: "));
            Assert.That(monitor, Does.Contain("case \"Y\":"));
            Assert.That(monitor, Does.Contain("return \"A\";"));
            Assert.That(tools, Does.Contain("Press F8 in Play Mode to show the overlay."));
        }

        [Test]
        public void ControllerInputMonitorRuntimeTypeIsLoadable()
        {
            var monitorType = System.Type.GetType("NumiDream.DebugTools.ControllerInputMonitor, Assembly-CSharp");

            Assert.That(monitorType, Is.Not.Null);
            Assert.That(
                monitorType.GetField("EnabledPlayerPrefsKey", BindingFlags.Public | BindingFlags.Static).GetRawConstantValue(),
                Is.EqualTo("NumiDream.ControllerInputMonitor.Enabled"));
            Assert.That(monitorType.GetMethod("EnsureExists", BindingFlags.Public | BindingFlags.Static), Is.Not.Null);
        }

        [Test]
        public void ControllerInputMonitorMenuItemsExist()
        {
            var toolsType = System.Type.GetType("DreamScripts.EditorTools.ControllerInputMonitorTools, Assembly-CSharp-Editor");

            Assert.That(toolsType, Is.Not.Null);

            var menuPaths = toolsType
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => method.GetCustomAttributes(typeof(MenuItem), inherit: false))
                .Select(attribute => (string)attribute.GetType().GetField("menuItem").GetValue(attribute))
                .ToArray();

            Assert.That(menuPaths, Does.Contain("DreamScripts/Project/Controller Input Monitor/Enable"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/Project/Controller Input Monitor/Disable"));
            Assert.That(menuPaths, Does.Contain("DreamScripts/Project/Controller Input Monitor/Status"));
        }
    }
}
