using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// Converts values authored for A-Frame / three.js into Unity space.
    /// </summary>
    /// <remarks>
    /// three.js is right-handed with the camera looking down -Z; Unity is left-handed with the camera looking
    /// down +Z. Mirroring the Z axis maps one onto the other while keeping the rendered picture identical, so
    /// every number from the original <c>index.html</c> can be copied verbatim and passed through these helpers.
    /// </remarks>
    public static class AFrame
    {
        /// <summary>A-Frame position (x, y, z) to Unity position (x, y, -z).</summary>
        public static Vector3 Position(float x, float y, float z)
        {
            return new Vector3(x, y, -z);
        }

        /// <summary>A-Frame position to Unity position.</summary>
        public static Vector3 Position(Vector3 aframePosition)
        {
            return new Vector3(aframePosition.x, aframePosition.y, -aframePosition.z);
        }

        /// <summary>
        /// A-Frame rotation in degrees to a Unity rotation. A-Frame 0.7 applies Euler angles in YXZ order,
        /// which is also Unity's order, so mirroring Z only flips the sign of the X and Y angles.
        /// </summary>
        public static Quaternion Rotation(float x, float y, float z)
        {
            return Quaternion.Euler(-x, -y, z);
        }

        /// <summary>A-Frame rotation in degrees to a Unity rotation.</summary>
        public static Quaternion Rotation(Vector3 aframeDegrees)
        {
            return Rotation(aframeDegrees.x, aframeDegrees.y, aframeDegrees.z);
        }

        /// <summary>
        /// Heading of a Unity rotation expressed the way A-Frame reports <c>rotation.y</c> (radians, positive
        /// when turned to the left).
        /// </summary>
        public static float Yaw(Quaternion unityRotation)
        {
            float unityYawDegrees = Mathf.DeltaAngle(0f, unityRotation.eulerAngles.y);
            return -unityYawDegrees * Mathf.Deg2Rad;
        }

        /// <summary>
        /// Parses a CSS colour such as <c>#a3d0ed</c>. The value is kept as authored (sRGB), which is what the
        /// Ergo shaders expect.
        /// </summary>
        public static Color Hex(string htmlColor)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(htmlColor, out color))
            {
                Debug.LogWarning("Ergo: could not parse colour '" + htmlColor + "', using white.");
                return Color.white;
            }

            return color;
        }
    }
}
