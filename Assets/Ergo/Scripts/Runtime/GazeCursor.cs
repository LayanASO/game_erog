using System.Collections.Generic;
using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// The mobile gaze cursor: <c>cursor="fuse: true; fuseTimeout: 250"</c> with a raycaster limited to 50 m and
    /// clickable objects. Looking at a button for the fuse timeout clicks it, and the ring plays the original
    /// "fusing" animation (scale 1 to 0.2, ease-in, 250 ms, then back to its resting size).
    /// </summary>
    public sealed class GazeCursor : MonoBehaviour
    {
        private const float MaxDistance = 50f;
        private const float FuseAnimationMs = 250f;

        private readonly List<ClickTarget> targets = new List<ClickTarget>();
        private Camera viewCamera;
        private float fuseTimeout = 0.25f;
        private ClickTarget hovered;
        private float hoverTime;
        private bool fired;
        private Vector3 restScale = Vector3.one;
        private float fuseAnimationMs = -1f;

        /// <summary>Target currently under the cursor, if any.</summary>
        public ClickTarget Hovered
        {
            get { return hovered; }
        }

        /// <summary>Sets the camera to cast from and the fuse timeout in seconds.</summary>
        public void Initialize(Camera castFrom, float fuseTimeoutSeconds)
        {
            viewCamera = castFrom;
            fuseTimeout = Mathf.Max(0f, fuseTimeoutSeconds);
            restScale = transform.localScale;
        }

        /// <summary>Adds an object the cursor can click.</summary>
        public void AddTarget(ClickTarget target)
        {
            if (target != null && !targets.Contains(target))
            {
                targets.Add(target);
            }
        }

        /// <summary>Nearest clickable target along <paramref name="ray"/>.</summary>
        public ClickTarget FindTarget(Ray ray)
        {
            ClickTarget best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                float distance;
                if (targets[i] != null && targets[i].Raycast(ray, MaxDistance, out distance) && distance < bestDistance)
                {
                    best = targets[i];
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Casts from the centre of the view, runs the fuse timer and animates the ring.</summary>
        public void Tick(float deltaTime)
        {
            if (viewCamera == null)
            {
                return;
            }

            Transform view = viewCamera.transform;
            ClickTarget target = FindTarget(new Ray(view.position, view.forward));
            if (target != hovered)
            {
                hovered = target;
                hoverTime = 0f;
                fired = false;
                if (target != null)
                {
                    fuseAnimationMs = 0f;
                }
            }

            if (hovered != null && !fired)
            {
                hoverTime += deltaTime;
                if (hoverTime >= fuseTimeout)
                {
                    fired = true;
                    hovered.Click();
                }
            }

            AnimateFuse(deltaTime);
        }

        private void AnimateFuse(float deltaTime)
        {
            if (fuseAnimationMs < 0f)
            {
                return;
            }

            fuseAnimationMs += deltaTime * 1000f;
            if (fuseAnimationMs >= FuseAnimationMs)
            {
                // fill="backwards" restores the value the cursor had before the animation.
                transform.localScale = restScale;
                fuseAnimationMs = -1f;
                return;
            }

            float k = Easing.Evaluate(EasingType.CubicIn, fuseAnimationMs / FuseAnimationMs);
            transform.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.2f, k);
        }
    }
}
