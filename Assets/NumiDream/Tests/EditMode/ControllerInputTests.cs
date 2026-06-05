using System.IO;
using NUnit.Framework;
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
        }
    }
}
