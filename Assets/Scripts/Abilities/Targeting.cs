using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>Target acquisition shared by tracking abilities.</summary>
    public static class Targeting
    {
        /// <summary>The living target whose direction is nearest the aim
        /// vector — smallest angle off it, inside the cone and range
        /// (maxRange 0 = unlimited). Null when nothing qualifies.</summary>
        public static Transform NearestToAim(Vector3 origin, Vector3 dir, float coneDegrees,
            float maxRange, GameObject ignore)
        {
            Transform best = null;
            float bestAngle = coneDegrees;
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead || h.gameObject == ignore) continue;
                Vector3 to = h.transform.position + Vector3.up - origin;
                if (maxRange > 0f && to.sqrMagnitude > maxRange * maxRange) continue;
                float angle = Vector3.Angle(dir, to);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = h.transform;
                }
            }
            return best;
        }
    }
}
