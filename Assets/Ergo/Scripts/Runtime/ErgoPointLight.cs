using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// The orange point light inside the player's ball. It is evaluated by the Ergo shaders the way three.js r87
    /// did with <c>distance: 0</c>: no falloff, so it tints everything facing the player.
    /// </summary>
    public sealed class ErgoPointLight : MonoBehaviour
    {
        /// <summary>Light colour (sRGB, as authored).</summary>
        public Color color = new Color32(0xFF, 0x44, 0x0C, 0xFF);

        /// <summary>Light intensity (animated between 0.35 and 0.5 in the original).</summary>
        public float intensity = 0.35f;
    }
}
