using System;
using UnityEngine;

namespace Ergo
{
    /// <summary>How the player steers.</summary>
    public enum ControlScheme
    {
        /// <summary>Mobile controls on phones and tablets, keyboard controls everywhere else (like the original).</summary>
        Automatic,

        /// <summary>Any key starts, A/D or the arrow keys change lane, drag with the mouse to look around.</summary>
        Desktop,

        /// <summary>Turn the device (or drag horizontally) to pick a lane; gaze at buttons to press them.</summary>
        Mobile,
    }

    /// <summary>
    /// Tunables for the game. The defaults are the values hard-coded in the original <c>runner.js</c> and
    /// <c>index.html</c>; positions are in A-Frame units inside the track (the original <c>#tree-container</c>).
    /// </summary>
    [Serializable]
    public sealed class ErgoSettings
    {
        [Header("Controls")]
        [Tooltip("Automatic uses mobile controls on phones and tablets, desktop controls elsewhere.")]
        public ControlScheme controlScheme = ControlScheme.Automatic;

        [Tooltip("Head/phone yaw in radians needed to move to a side lane (original lane-controls: 0.1).")]
        public float headTurnThreshold = 0.1f;

        [Tooltip("Seconds of gazing at a button before it is pressed (original cursor fuseTimeout: 250 ms).")]
        public float fuseTimeout = 0.25f;

        [Tooltip("Mouse-drag look speed in radians per pixel (A-Frame look-controls: 0.002).")]
        public float mouseLookSpeed = 0.002f;

        [Tooltip("Rotate the camera with the gyroscope on mobile, like the original magic-window mode.")]
        public bool useGyroscope = true;

        [Tooltip("Mouse/touch/gyro look. Turn off when something else, such as an XR rig, drives the camera.")]
        public bool lookControls = true;

        [Header("Lanes")]
        [Tooltip("Lane X positions: left, centre, right (POSITION_X_LEFT/CENTER/RIGHT).")]
        public float[] laneX = { -0.5f, 0f, 0.5f };

        [Tooltip("Tree Y positions per lane, from the three tree templates.")]
        public float[] treeLaneY = { 0.55f, 0.6f, 0.55f };

        [Header("Trees")]
        [Tooltip("Seconds between rows of trees (addTreesRandomlyLoop intervalLength: 500 ms).")]
        public float treeSpawnInterval = 0.5f;

        [Tooltip("Chance of a tree in the left, centre and right lane for each row.")]
        public float[] laneTreeProbability = { 0.5f, 0.5f, 0.5f };

        [Tooltip("Maximum trees per row, so there is always a free lane.")]
        public int maxTreesPerRow = 2;

        [Tooltip("Tree Z where a tree appears.")]
        public float treeStartZ = -7f;

        [Tooltip("Tree Z where the tree animation ends.")]
        public float treeEndZ = 1.5f;

        [Tooltip("Seconds for a tree to travel from start to end (a-animation dur: 5000).")]
        public float treeTravelTime = 5f;

        [Tooltip("Tree motion curve. The original played A-Frame's default 'ease' (cubic in-out).")]
        public EasingType treeEasing = Easing.AFrameDefault;

        [Tooltip("Trees past this Z are removed (POSITION_Z_OUT_OF_SIGHT).")]
        public float treeRemoveZ = 1f;

        [Tooltip("A tree in the player's lane between these Z values ends the game (POSITION_Z_LINE_START/END).")]
        public float collisionZStart = 0.6f;

        [Tooltip("Trees past this Z score a point.")]
        public float collisionZEnd = 0.7f;

        [Header("Look")]
        public Color skyColor = new Color32(0xA3, 0xD0, 0xED, 0xFF);
        public float fogNear = 5f;
        public float fogFar = 20f;
        public Color ambientColor = new Color32(0xB4, 0xC5, 0xEC, 0xFF);
        public float ambientIntensity = 0.8f;
        public Color sunColor = new Color32(0xD0, 0xEA, 0xF9, 0xFF);
        public float sunIntensity = 0.4f;

        [Tooltip("Directional light position in A-Frame world space; it shines towards the origin.")]
        public Vector3 sunPosition = new Vector3(5f, 3f, 1f);

        public Color playerLightColor = new Color32(0xFF, 0x44, 0x0C, 0xFF);

        [Tooltip("Directional shadows (the original used a 512px PCF shadow map).")]
        public bool shadows = true;

        public int shadowMapSize = 512;

        /// <summary>Probability of a tree in <paramref name="lane"/> (0.5 if the array is too short).</summary>
        public float TreeProbability(int lane)
        {
            return laneTreeProbability != null && lane < laneTreeProbability.Length ? laneTreeProbability[lane] : 0.5f;
        }

        /// <summary>Lane X position (defaults to the original lanes if the array is too short).</summary>
        public float LaneX(int lane)
        {
            return laneX != null && lane < laneX.Length ? laneX[lane] : (lane - 1) * 0.5f;
        }

        /// <summary>Tree Y position for <paramref name="lane"/>.</summary>
        public float TreeY(int lane)
        {
            return treeLaneY != null && lane < treeLaneY.Length ? treeLaneY[lane] : (lane == 1 ? 0.6f : 0.55f);
        }
    }
}
