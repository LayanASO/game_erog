using System;
using UnityEngine;

namespace Ergo
{
    /// <summary>
    /// A box that can be "clicked" by the gaze cursor or a tap, like the original start/restart buttons (an
    /// <c>a-box</c> with the <c>clickable</c> class toggled by the menu code).
    /// </summary>
    public sealed class ClickTarget : MonoBehaviour
    {
        [Tooltip("Box size in local units (the a-box width, height and depth).")]
        public Vector3 size = Vector3.one;

        [Tooltip("Only clickable targets react (the original toggled a 'clickable' class).")]
        public bool clickable;

        /// <summary>Raised when the target is clicked.</summary>
        public event Action Clicked;

        /// <summary>Intersects a world-space ray with the box.</summary>
        public bool Raycast(Ray ray, float maxDistance, out float distance)
        {
            distance = 0f;
            if (!clickable || !isActiveAndEnabled)
            {
                return false;
            }

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Vector3 localDirection = worldToLocal.MultiplyVector(ray.direction);
            if (localDirection.sqrMagnitude < 1e-12f)
            {
                return false;
            }

            var localRay = new Ray(worldToLocal.MultiplyPoint3x4(ray.origin), localDirection);
            float localDistance;
            if (!new Bounds(Vector3.zero, size).IntersectRay(localRay, out localDistance))
            {
                return false;
            }

            Vector3 worldHit = transform.localToWorldMatrix.MultiplyPoint3x4(localRay.GetPoint(localDistance));
            distance = Vector3.Distance(ray.origin, worldHit);
            return distance <= maxDistance;
        }

        /// <summary>Clicks the target.</summary>
        public void Click()
        {
            var clicked = Clicked;
            if (clicked != null)
            {
                clicked();
            }
        }
    }
}
