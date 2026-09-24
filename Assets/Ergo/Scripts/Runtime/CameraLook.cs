using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// Port of A-Frame 0.7's look-controls. On desktop, dragging with the left mouse button turns the view
    /// (0.002 radians per pixel, pitch clamped to straight up/down). On mobile the device orientation drives the
    /// camera ("magic window") and horizontal drags add extra yaw. Turn <see cref="ErgoSettings.lookControls"/>
    /// off when something else (for example an XR rig) drives the camera.
    /// </summary>
    public sealed class CameraLook : MonoBehaviour
    {
        private ErgoSettings settings;
        private bool mobile;
        private bool useAttitude;
        private float yawDegrees;
        private float pitchDegrees;
        private float touchYawDegrees;
        private bool dragging;
        private Vector2 lastPointer;
        private bool recentered;
        private Quaternion recenter = Quaternion.identity;
        private Quaternion deviceRotation = Quaternion.identity;

        /// <summary>Sets the control style and starts the orientation sensor on mobile.</summary>
        public void Initialize(ErgoSettings gameSettings, bool mobileControls)
        {
            settings = gameSettings;
            mobile = mobileControls;
            useAttitude = mobile && settings.useGyroscope && ErgoInput.EnableAttitudeSensor();
        }

        /// <summary>Makes the current device heading the new "forward".</summary>
        public void Recenter()
        {
            recentered = false;
            touchYawDegrees = 0f;
        }

        /// <summary>Updates the camera rotation for this frame.</summary>
        public void Tick()
        {
            if (settings == null || !settings.lookControls)
            {
                return;
            }

            Vector2 delta = PointerDelta();
            if (mobile)
            {
                // look-controls onTouchMove: yaw -= 0.5 * 2 * PI * dx / canvasWidth.
                if (Screen.width > 0)
                {
                    touchYawDegrees += 180f * delta.x / Screen.width;
                }

                Quaternion attitude;
                if (useAttitude && ErgoInput.TryGetAttitude(out attitude))
                {
                    // Right-handed, Z-up sensor frame to a Unity camera looking out of the back of the device.
                    Quaternion rotation = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(attitude.x, attitude.y, -attitude.z, -attitude.w);
                    if (!recentered)
                    {
                        TryRecenter(rotation);
                    }

                    if (recentered)
                    {
                        deviceRotation = recenter * rotation;
                    }
                }

                transform.localRotation = Quaternion.Euler(0f, touchYawDegrees, 0f) * deviceRotation;
            }
            else
            {
                float degreesPerPixel = settings.mouseLookSpeed * Mathf.Rad2Deg;
                yawDegrees += delta.x * degreesPerPixel;
                pitchDegrees = Mathf.Clamp(pitchDegrees - delta.y * degreesPerPixel, -90f, 90f);
                transform.localRotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            }
        }

        private Vector2 PointerDelta()
        {
            Vector2 position;
            bool pressedThisFrame;
            bool held = ErgoInput.GetPointer(out position, out pressedThisFrame);
            if (!held)
            {
                dragging = false;
                return Vector2.zero;
            }

            Vector2 delta = dragging && !pressedThisFrame ? position - lastPointer : Vector2.zero;
            dragging = true;
            lastPointer = position;
            return delta;
        }

        // Like the WebVR polyfill, start facing the scene whatever direction the device points at launch.
        private void TryRecenter(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            var flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.05f)
            {
                // Pointing straight up or down; wait for a sample with a usable heading.
                return;
            }

            recenter = Quaternion.Euler(0f, -Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 0f);
            recentered = true;
        }
    }
}
