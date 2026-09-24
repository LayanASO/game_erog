using UnityEngine;
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ergo
{
    /// <summary>
    /// The few inputs Ergo needs, read from the Input System package when it is active and from the legacy
    /// Input Manager otherwise, so the project works with either "Active Input Handling" setting.
    /// </summary>
    public static class ErgoInput
    {
        /// <summary>True on the frame any keyboard key goes down ("Hit any key to start").</summary>
        public static bool AnyKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2);
#else
            return false;
#endif
        }

        /// <summary>True on the frame the left arrow or A goes down.</summary>
        public static bool LeftPressed()
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);
#else
            return false;
#endif
        }

        /// <summary>True on the frame the right arrow or D goes down.</summary>
        public static bool RightPressed()
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#else
            return false;
#endif
        }

        /// <summary>
        /// State of the primary pointer (left mouse button or first touch) in screen pixels.
        /// </summary>
        public static bool GetPointer(out Vector2 position, out bool pressedThisFrame)
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var pointer = Pointer.current;
            if (pointer == null)
            {
                position = Vector2.zero;
                pressedThisFrame = false;
                return false;
            }

            position = pointer.position.ReadValue();
            pressedThisFrame = pointer.press.wasPressedThisFrame;
            return pointer.press.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Touches also arrive here because Input.simulateMouseWithTouches is on by default.
            position = Input.mousePosition;
            pressedThisFrame = Input.GetMouseButtonDown(0);
            return Input.GetMouseButton(0);
#else
            position = Vector2.zero;
            pressedThisFrame = false;
            return false;
#endif
        }

        /// <summary>Turns on the device orientation sensor if there is one.</summary>
        public static bool EnableAttitudeSensor()
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var sensor = AttitudeSensor.current;
            if (sensor == null)
            {
                return false;
            }

            if (!sensor.enabled)
            {
                InputSystem.EnableDevice(sensor);
            }

            return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!SystemInfo.supportsGyroscope)
            {
                return false;
            }

            Input.gyro.enabled = true;
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Device orientation in the sensor's right-handed frame, already compensated for the screen orientation
        /// (the Input System does this itself; for the legacy gyroscope the same correction is applied here).
        /// Returns false until the sensor reports a value.
        /// </summary>
        public static bool TryGetAttitude(out Quaternion attitude)
        {
#if ENABLE_INPUT_SYSTEM && ERGO_INPUT_SYSTEM
            var sensor = AttitudeSensor.current;
            if (sensor == null || !sensor.enabled)
            {
                attitude = Quaternion.identity;
                return false;
            }

            attitude = sensor.attitude.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!SystemInfo.supportsGyroscope || !Input.gyro.enabled)
            {
                attitude = Quaternion.identity;
                return false;
            }

            attitude = Input.gyro.attitude * ScreenOrientationCompensation();
#else
            attitude = Quaternion.identity;
#endif
            return IsValidSample(ref attitude);
        }

        // Sensors report a zero or identity rotation until their first sample arrives.
        private static bool IsValidSample(ref Quaternion attitude)
        {
            float lengthSquared = attitude.x * attitude.x + attitude.y * attitude.y + attitude.z * attitude.z + attitude.w * attitude.w;
            if (float.IsNaN(lengthSquared) || lengthSquared < 0.5f || lengthSquared > 1.5f)
            {
                attitude = Quaternion.identity;
                return false;
            }

            if (Mathf.Abs(attitude.w) > 0.9999999f)
            {
                return false;
            }

            attitude = Quaternion.Normalize(attitude);
            return true;
        }

        // Same rotation the Input System's CompensateRotation processor applies.
        private static Quaternion ScreenOrientationCompensation()
        {
            const float halfSqrtTwo = 0.70710678f;
            switch (Screen.orientation)
            {
                case ScreenOrientation.PortraitUpsideDown:
                    return new Quaternion(0f, 0f, 1f, 0f);
                case ScreenOrientation.LandscapeLeft:
                    return new Quaternion(0f, 0f, halfSqrtTwo, -halfSqrtTwo);
                case ScreenOrientation.LandscapeRight:
                    return new Quaternion(0f, 0f, -halfSqrtTwo, -halfSqrtTwo);
                default:
                    return Quaternion.identity;
            }
        }
    }
}
