using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NumiDream.Input
{
    public static class NumiInput
    {
        public const float DefaultDeadZone = 0.18f;

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
            if (joystick != null && joystick.trigger.wasPressedThisFrame)
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
            if (joystick != null && joystick.trigger.isPressed)
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
            if (joystick != null && joystick.trigger.wasReleasedThisFrame)
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
            if (joystick != null && joystick.trigger.wasPressedThisFrame)
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
            if (joystick != null && joystick.trigger.isPressed)
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
