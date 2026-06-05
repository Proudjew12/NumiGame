using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace NumiDream.Input
{
    public static class NumiInput
    {
        public const float DefaultDeadZone = 0.18f;
        public const float EightBitDoAxisPressThreshold = 0.55f;

        private const float EightBitDoAxisReleaseThreshold = 0.22f;

#if ENABLE_INPUT_SYSTEM
        private static bool s_eightBitDoRightTriggerAxisPressed;
        private static bool s_eightBitDoLeftTriggerAxisPressed;
        private static int s_eightBitDoRightTriggerAxisPressedFrame = -1;
        private static int s_eightBitDoLeftTriggerAxisPressedFrame = -1;
#endif

        public static float ReadHorizontal(float deadZone = DefaultDeadZone)
        {
            var horizontal = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                var keyboardHorizontal = 0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    keyboardHorizontal -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    keyboardHorizontal += 1f;
                }

                horizontal = Strongest(horizontal, keyboardHorizontal);
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                horizontal = Strongest(horizontal, ApplyDeadZone(gamepad.leftStick.ReadValue().x, deadZone));
                horizontal = Strongest(horizontal, gamepad.dpad.ReadValue().x);
            }

            var joystick = Joystick.current;
            if (joystick != null)
            {
                horizontal = Strongest(horizontal, ApplyDeadZone(joystick.stick.ReadValue().x, deadZone));
                if (joystick.hatswitch != null)
                {
                    horizontal = Strongest(horizontal, joystick.hatswitch.ReadValue().x);
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = Strongest(horizontal, ApplyDeadZone(UnityEngine.Input.GetAxisRaw("Horizontal"), deadZone));
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal = Strongest(horizontal, -1f);
            }

            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                horizontal = Strongest(horizontal, 1f);
            }
#endif

            return Mathf.Clamp(horizontal, -1f, 1f);
        }

        public static bool WasJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.wKey.wasPressedThisFrame ||
                 keyboard.upArrowKey.wasPressedThisFrame))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.wasPressedThisFrame ||
                 gamepad.dpad.up.wasPressedThisFrame))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (WasGenericJoystickTriggerPressed(joystick) ||
                 WasNamedButtonPressed(joystick, "Y")))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetButtonDown("Jump") ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.W) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        public static bool IsJumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.isPressed ||
                 keyboard.wKey.isPressed ||
                 keyboard.upArrowKey.isPressed))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.isPressed ||
                 gamepad.dpad.up.isPressed))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (IsGenericJoystickTriggerHeld(joystick) ||
                 IsNamedButtonHeld(joystick, "Y")))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetButton("Jump") ||
                   UnityEngine.Input.GetKey(KeyCode.Space) ||
                   UnityEngine.Input.GetKey(KeyCode.W) ||
                   UnityEngine.Input.GetKey(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        public static bool WasJumpReleased()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasReleasedThisFrame ||
                 keyboard.wKey.wasReleasedThisFrame ||
                 keyboard.upArrowKey.wasReleasedThisFrame))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonSouth.wasReleasedThisFrame ||
                 gamepad.dpad.up.wasReleasedThisFrame))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (WasGenericJoystickTriggerReleased(joystick) ||
                 WasNamedButtonReleased(joystick, "Y")))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetButtonUp("Jump") ||
                   UnityEngine.Input.GetKeyUp(KeyCode.Space) ||
                   UnityEngine.Input.GetKeyUp(KeyCode.W) ||
                   UnityEngine.Input.GetKeyUp(KeyCode.UpArrow);
#else
            return false;
#endif
        }

        public static bool WasPuzzleActionPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonNorth.wasPressedThisFrame ||
                 gamepad.rightShoulder.wasPressedThisFrame ||
                 gamepad.rightTrigger.wasPressedThisFrame))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (WasGenericJoystickTriggerPressed(joystick) ||
                 Was8BitDoRightActionPressed(joystick) ||
                 Was8BitDoLeftActionPressed(joystick)))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.T);
#else
            return false;
#endif
        }

        public static bool IsPuzzleActionHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.isPressed)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonNorth.isPressed ||
                 gamepad.rightShoulder.isPressed ||
                 gamepad.rightTrigger.isPressed))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (IsGenericJoystickTriggerHeld(joystick) ||
                 Is8BitDoRightActionHeld(joystick) ||
                 Is8BitDoLeftActionHeld(joystick)))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKey(KeyCode.T);
#else
            return false;
#endif
        }

        public static bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null &&
                (gamepad.buttonNorth.wasPressedThisFrame ||
                 gamepad.rightShoulder.wasPressedThisFrame ||
                 gamepad.rightTrigger.wasPressedThisFrame))
            {
                return true;
            }

            var joystick = Joystick.current;
            if (joystick != null &&
                (WasGenericJoystickTriggerPressed(joystick) ||
                 Was8BitDoRightActionPressed(joystick)))
            {
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return UnityEngine.Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        public static float ReadScaleInput()
        {
            var scaleInput = 0f;

#if ENABLE_INPUT_SYSTEM
            var joystick = Joystick.current;
            if (joystick != null)
            {
                if (Is8BitDoRightActionHeld(joystick))
                {
                    scaleInput += 1f;
                }

                if (Is8BitDoLeftActionHeld(joystick))
                {
                    scaleInput -= 1f;
                }
            }
#endif

            return Mathf.Clamp(scaleInput, -1f, 1f);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool Was8BitDoRightActionPressed(Joystick joystick)
        {
            return WasNamedButtonPressed(joystick, "TriggerRight") ||
                   WasNamedButtonPressed(joystick, "Start") ||
                   WasFilteredAxisPressed(
                       joystick,
                       "RotateZ",
                       ref s_eightBitDoRightTriggerAxisPressed,
                       ref s_eightBitDoRightTriggerAxisPressedFrame,
                       blockWhenSelectHeld: true);
        }

        private static bool Is8BitDoRightActionHeld(Joystick joystick)
        {
            return IsNamedButtonHeld(joystick, "TriggerRight") ||
                   IsNamedButtonHeld(joystick, "Start") ||
                   IsFilteredAxisHeld(joystick, "RotateZ", blockWhenSelectHeld: true);
        }

        private static bool Was8BitDoLeftActionPressed(Joystick joystick)
        {
            return WasNamedButtonPressed(joystick, "TriggerLeft2") ||
                   WasNamedButtonPressed(joystick, "Select") ||
                   WasFilteredAxisPressed(
                       joystick,
                       "Z",
                       ref s_eightBitDoLeftTriggerAxisPressed,
                       ref s_eightBitDoLeftTriggerAxisPressedFrame,
                       blockWhenSelectHeld: false);
        }

        private static bool Is8BitDoLeftActionHeld(Joystick joystick)
        {
            return IsNamedButtonHeld(joystick, "TriggerLeft2") ||
                   IsNamedButtonHeld(joystick, "Select") ||
                   IsFilteredAxisHeld(joystick, "Z", blockWhenSelectHeld: false);
        }

        private static bool WasNamedButtonPressed(InputDevice device, string controlName)
        {
            var button = device.TryGetChildControl<ButtonControl>(controlName);
            return button != null && button.wasPressedThisFrame;
        }

        private static bool IsNamedButtonHeld(InputDevice device, string controlName)
        {
            var button = device.TryGetChildControl<ButtonControl>(controlName);
            return button != null && button.isPressed;
        }

        private static bool WasNamedButtonReleased(InputDevice device, string controlName)
        {
            var button = device.TryGetChildControl<ButtonControl>(controlName);
            return button != null && button.wasReleasedThisFrame;
        }

        private static bool WasGenericJoystickTriggerPressed(Joystick joystick)
        {
            return !IsNamedButtonHeld(joystick, "Select") &&
                   joystick.trigger != null &&
                   joystick.trigger.wasPressedThisFrame;
        }

        private static bool IsGenericJoystickTriggerHeld(Joystick joystick)
        {
            return !IsNamedButtonHeld(joystick, "Select") &&
                   joystick.trigger != null &&
                   joystick.trigger.isPressed;
        }

        private static bool WasGenericJoystickTriggerReleased(Joystick joystick)
        {
            return !IsNamedButtonHeld(joystick, "Select") &&
                   joystick.trigger != null &&
                   joystick.trigger.wasReleasedThisFrame;
        }

        private static bool WasFilteredAxisPressed(
            Joystick joystick,
            string controlName,
            ref bool wasPressed,
            ref int pressedFrame,
            bool blockWhenSelectHeld)
        {
            if (blockWhenSelectHeld && IsNamedButtonHeld(joystick, "Select"))
            {
                wasPressed = false;
                pressedFrame = -1;
                return false;
            }

            var isHeld = IsNamedAxisHeld(joystick, controlName, wasPressed);
            if (!isHeld)
            {
                wasPressed = false;
                pressedFrame = -1;
                return false;
            }

            if (wasPressed)
            {
                return pressedFrame == Time.frameCount;
            }

            wasPressed = true;
            pressedFrame = Time.frameCount;
            return true;
        }

        private static bool IsFilteredAxisHeld(Joystick joystick, string controlName, bool blockWhenSelectHeld)
        {
            return (!blockWhenSelectHeld || !IsNamedButtonHeld(joystick, "Select")) &&
                   IsNamedAxisHeld(joystick, controlName, alreadyPressed: true);
        }

        private static bool IsNamedAxisHeld(InputDevice device, string controlName, bool alreadyPressed)
        {
            var axis = device.TryGetChildControl<AxisControl>(controlName);
            if (axis == null)
            {
                return false;
            }

            var value = Mathf.Abs(axis.ReadValue());
            return value >= (alreadyPressed ? EightBitDoAxisReleaseThreshold : EightBitDoAxisPressThreshold);
        }
#endif

        private static float ApplyDeadZone(float value, float deadZone)
        {
            return Mathf.Abs(value) >= Mathf.Max(0f, deadZone) ? value : 0f;
        }

        private static float Strongest(float current, float candidate)
        {
            return Mathf.Abs(candidate) > Mathf.Abs(current) ? candidate : current;
        }
    }
}
