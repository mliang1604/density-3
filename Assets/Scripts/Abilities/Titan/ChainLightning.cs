using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Arc chain shared by the Titan kit: from a struck enemy, lightning
    /// jumps to the nearest other living target within reach, up to maxJumps
    /// times — beam, burst, zap, and flat damage per link. Ability zones
    /// (mines, barricades) and the caster are never targets; each enemy is
    /// struck at most once per chain.
    /// </summary>
    public static class ChainLightning
    {
        public const float DefaultReach = 6f;
        public const int DefaultMaxJumps = 3;

        public static void Chain(Health origin, float damagePerJump, GameObject source,
            float reach = DefaultReach, int maxJumps = DefaultMaxJumps)
        {
            if (origin == null) return;
            var struck = new HashSet<Health> { origin }; // rare event; a set per chain is fine
            Health current = origin;
            for (int jump = 0; jump < maxJumps; jump++)
            {
                Health next = Nearest(current.transform.position, reach, struck, source);
                if (next == null) return;
                struck.Add(next);

                Vector3 a = current.transform.position + Vector3.up;
                Vector3 b = next.transform.position + Vector3.up;
                FX.SpawnBeam(a, b, Element.Arc, 0.06f, 0.3f);
                FX.SpawnElementBurst(b, Element.Arc, 0.5f);
                SFX.Play3D(SFX.ArcZapClip, b, 0.6f, 5f);

                next.ApplyDamage(new DamageInfo
                {
                    amount = damagePerJump,
                    isCrit = false,
                    hitPoint = b,
                    source = source
                });
                DamageNumbers.Spawn(b, damagePerJump, false);
                current = next;
            }
        }

        /// <summary>The nearest valid chain target to a point — used by AoE
        /// abilities to pick the chain's first victim.</summary>
        public static Health NearestTarget(Vector3 center, float reach, GameObject source)
            => Nearest(center, reach, null, source);

        private static Health Nearest(Vector3 center, float reach,
            HashSet<Health> exclude, GameObject source)
        {
            Health best = null;
            float bestSqr = reach * reach;
            foreach (var h in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead || h.gameObject == source) continue;
                if (exclude != null && exclude.Contains(h)) continue;
                // Player-built zones are not enemies.
                if (h.GetComponent<TripmineZone>() != null || h.GetComponent<BarricadeZone>() != null)
                    continue;
                float sqr = (h.transform.position - center).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = h;
                }
            }
            return best;
        }
    }
}
