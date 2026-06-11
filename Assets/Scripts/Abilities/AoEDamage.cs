using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Radial damage for ability detonations. Overlaps a sphere, routes one
    /// DamageInfo per distinct Health (a rig's many colliders count once),
    /// and skips the source's own Health so casters can't nuke themselves.
    /// No line-of-sight check — callers gate placement instead. Buffers are
    /// reused; no per-call allocations.
    /// </summary>
    public static class AoEDamage
    {
        private static readonly Collider[] overlaps = new Collider[64];
        private static readonly HashSet<Health> seen = new HashSet<Health>();

        /// <summary>Returns how many targets were damaged.</summary>
        public static int Apply(Vector3 center, float radius, float damage, GameObject source,
            bool showNumbers = true)
            => Apply(center, radius, damage, source, showNumbers, null);

        /// <summary>As Apply, additionally appending targets killed by this
        /// application to killed (the list is not cleared here) — chain
        /// mechanics read it to keep detonating.</summary>
        public static int Apply(Vector3 center, float radius, float damage, GameObject source,
            bool showNumbers, List<Health> killed)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, overlaps, ~0,
                QueryTriggerInteraction.Ignore);
            seen.Clear();
            int damaged = 0;
            for (int i = 0; i < n; i++)
            {
                var health = overlaps[i].GetComponentInParent<Health>();
                if (health == null || health.IsDead || !seen.Add(health)) continue;
                if (source != null && health.gameObject == source) continue;

                Vector3 at = overlaps[i].ClosestPoint(center);
                health.ApplyDamage(new DamageInfo
                {
                    amount = damage,
                    isCrit = false,
                    hitPoint = at,
                    source = source
                });
                if (showNumbers) DamageNumbers.Spawn(at, damage, false);
                if (killed != null && health.IsDead) killed.Add(health);
                damaged++;
            }
            return damaged;
        }
    }
}
