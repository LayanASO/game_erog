using UnityEngine;

namespace Ergo
{
    /// <summary>Property driven by an <see cref="AFrameAnimation"/>.</summary>
    public enum AnimatedProperty
    {
        /// <summary>Local position, given in A-Frame coordinates.</summary>
        Position,

        /// <summary>Local rotation, given as A-Frame Euler angles in degrees.</summary>
        Rotation,

        /// <summary>Local scale.</summary>
        Scale,

        /// <summary>Intensity of an <see cref="ErgoPointLight"/> on the same object (uses the X component).</summary>
        LightIntensity,
    }

    /// <summary>Playback direction, as in the <c>direction</c> attribute of &lt;a-animation&gt;.</summary>
    public enum AnimationDirection
    {
        Normal,
        Reverse,
        Alternate,
        AlternateReverse,
    }

    /// <summary>
    /// Port of A-Frame 0.7's &lt;a-animation&gt; for the looping animations in the original scene (icebergs
    /// bobbing, the player's ball pulsing, its light flickering). Only <c>fill="forwards"</c> is supported, which
    /// is the default and the only fill mode those animations use.
    /// </summary>
    public sealed class AFrameAnimation : MonoBehaviour
    {
        [SerializeField] private AnimatedProperty property = AnimatedProperty.Position;
        [SerializeField] private Vector3 from;
        [SerializeField] private Vector3 to;
        [SerializeField] private float durationMs = 1000f;
        [SerializeField] private EasingType easing = Easing.AFrameDefault;
        [SerializeField] private AnimationDirection direction = AnimationDirection.Normal;
        [SerializeField] private bool repeatIndefinitely;

        private float elapsedMs;
        private ErgoPointLight pointLight;

        /// <summary>Configures the animation (mirrors the attributes of an &lt;a-animation&gt; tag) and applies its first frame.</summary>
        public AFrameAnimation Configure(AnimatedProperty animatedProperty, Vector3 fromValue, Vector3 toValue, float durationInMs, EasingType easingType, AnimationDirection playDirection, bool indefinite)
        {
            property = animatedProperty;
            from = fromValue;
            to = toValue;
            durationMs = durationInMs;
            easing = easingType;
            direction = playDirection;
            repeatIndefinitely = indefinite;
            elapsedMs = 0f;
            Apply(Sample(0f));
            return this;
        }

        /// <summary>Value of the animation <paramref name="timeMs"/> milliseconds after it started.</summary>
        public Vector3 Sample(float timeMs)
        {
            float duration = Mathf.Max(durationMs, 0.001f);
            float time = Mathf.Max(0f, timeMs);
            int leg = Mathf.FloorToInt(time / duration);
            float k = (time - leg * duration) / duration;

            if (!repeatIndefinitely && leg >= 1)
            {
                // fill="forwards": hold the final value.
                leg = 0;
                k = 1f;
            }

            // "reverse" and "alternate-reverse" swap from/to; with repeat="indefinite" the alternating directions
            // become a Tween.js yoyo, flipping every leg.
            bool reversed = direction == AnimationDirection.Reverse || direction == AnimationDirection.AlternateReverse;
            bool alternates = direction == AnimationDirection.Alternate || direction == AnimationDirection.AlternateReverse;
            if (alternates && repeatIndefinitely && (leg & 1) == 1)
            {
                reversed = !reversed;
            }

            Vector3 start = reversed ? to : from;
            Vector3 end = reversed ? from : to;
            return Vector3.LerpUnclamped(start, end, Easing.Evaluate(easing, Mathf.Clamp01(k)));
        }

        private void Update()
        {
            elapsedMs += Time.deltaTime * 1000f;
            if (repeatIndefinitely)
            {
                // Two legs make a full cycle; wrapping keeps float precision over long sessions.
                elapsedMs %= 2f * Mathf.Max(durationMs, 0.001f);
            }

            Apply(Sample(elapsedMs));
        }

        private void Apply(Vector3 value)
        {
            switch (property)
            {
                case AnimatedProperty.Position:
                    transform.localPosition = AFrame.Position(value);
                    break;
                case AnimatedProperty.Rotation:
                    transform.localRotation = AFrame.Rotation(value);
                    break;
                case AnimatedProperty.Scale:
                    transform.localScale = value;
                    break;
                case AnimatedProperty.LightIntensity:
                    if (pointLight == null)
                    {
                        pointLight = GetComponent<ErgoPointLight>();
                    }

                    if (pointLight != null)
                    {
                        pointLight.intensity = value.x;
                    }

                    break;
            }
        }
    }
}
