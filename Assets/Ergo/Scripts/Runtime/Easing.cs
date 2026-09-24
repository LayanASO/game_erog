namespace Ergo
{
    /// <summary>Easing curves from Tween.js, the tweening library behind A-Frame 0.7's &lt;a-animation&gt;.</summary>
    public enum EasingType
    {
        Linear,
        CubicIn,
        CubicOut,
        CubicInOut,
    }

    /// <summary>Evaluates <see cref="EasingType"/> curves.</summary>
    public static class Easing
    {
        /// <summary>
        /// A-Frame's default easing, <c>ease</c>, is Tween.js Cubic.InOut. The original trees use it too: their
        /// markup says <c>ease="linear"</c>, but A-Frame only reads <c>easing</c>, so the attribute is ignored.
        /// </summary>
        public const EasingType AFrameDefault = EasingType.CubicInOut;

        /// <summary>Evaluates the curve at <paramref name="k"/> in [0, 1].</summary>
        public static float Evaluate(EasingType type, float k)
        {
            switch (type)
            {
                case EasingType.CubicIn:
                    return k * k * k;
                case EasingType.CubicOut:
                    k -= 1f;
                    return k * k * k + 1f;
                case EasingType.CubicInOut:
                    k *= 2f;
                    if (k < 1f)
                    {
                        return 0.5f * k * k * k;
                    }

                    k -= 2f;
                    return 0.5f * (k * k * k + 2f);
                default:
                    return k;
            }
        }
    }
}
