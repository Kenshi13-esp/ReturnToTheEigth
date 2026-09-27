using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ReturnToTheEigth.UI
{
    /// <summary>Tracks the most recently active keyboard/mouse or gamepad and provides matching control labels.</summary>
    public static class InputPromptUtility
    {
        private const float ActivityMagnitudeThreshold = 0.0001f;
        private const string KeyboardMoveLabel = "WASD";
        private const string GamepadMoveLabel = "Joystick / cruceta";
        private const string KeyboardInteractLabel = "E";
        private const string GamepadInteractLabel = "X";
        private const string KeyboardTimeShiftLabel = "R";
        private const string GamepadTimeShiftLabel = "Cuadrado";
        private const string KeyboardPuzzleExitLabel = "Q";
        private const string GamepadPuzzleExitLabel = "Círculo";
        private const string KeyboardNavigationLabel = "A/D";
        private const string GamepadNavigationLabel = "Cruceta izquierda/derecha";
        private static bool isInitialized;
        private static InputDevice lastUsedDevice;
        private static bool isGamepadActive;

        /// <summary>Gets whether the most recently active supported input device is a gamepad.</summary>
        public static bool IsGamepadActive => isGamepadActive;

        /// <summary>Gets the current movement control label.</summary>
        public static string MoveControlLabel => GetCurrentDeviceLabel(KeyboardMoveLabel, GamepadMoveLabel);

        /// <summary>Gets the current interact control label.</summary>
        public static string InteractControlLabel => GetCurrentDeviceLabel(KeyboardInteractLabel, GamepadInteractLabel);

        /// <summary>Gets the current time-shift control label.</summary>
        public static string TimeShiftControlLabel => GetCurrentDeviceLabel(KeyboardTimeShiftLabel, GamepadTimeShiftLabel);

        /// <summary>Gets the current puzzle-exit control label.</summary>
        public static string PuzzleExitControlLabel => GetCurrentDeviceLabel(KeyboardPuzzleExitLabel, GamepadPuzzleExitLabel);

        /// <summary>Gets the current piano-navigation control label.</summary>
        public static string NavigationControlLabel => GetCurrentDeviceLabel(KeyboardNavigationLabel, GamepadNavigationLabel);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (isInitialized)
            {
                InputSystem.onEvent -= HandleInputEvent;
                InputSystem.onDeviceChange -= HandleDeviceChange;
            }

            isInitialized = true;
            lastUsedDevice = null;
            isGamepadActive = false;
            InputSystem.onEvent += HandleInputEvent;
            InputSystem.onDeviceChange += HandleDeviceChange;
        }

        private static string GetCurrentDeviceLabel(string keyboardLabel, string gamepadLabel)
        {
            return isGamepadActive ? gamepadLabel : keyboardLabel;
        }

        private static void HandleInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (eventPtr.type != StateEvent.Type && eventPtr.type != DeltaStateEvent.Type)
            {
                return;
            }

            bool isGamepad = device is Gamepad;
            if (!isGamepad && device is not Keyboard && device is not Mouse)
            {
                return;
            }

            foreach (InputControl control in eventPtr.EnumerateChangedControls(device, ActivityMagnitudeThreshold))
            {
                if (control.EvaluateMagnitude() <= ActivityMagnitudeThreshold)
                {
                    continue;
                }

                lastUsedDevice = device;
                isGamepadActive = isGamepad;
                return;
            }
        }

        private static void HandleDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device != lastUsedDevice ||
                (change != InputDeviceChange.Removed && change != InputDeviceChange.Disconnected))
            {
                return;
            }

            lastUsedDevice = null;
            isGamepadActive = false;
        }
    }
}
