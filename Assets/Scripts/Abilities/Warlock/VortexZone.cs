using System.Collections.Generic;
using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The lingering vortex a Vortex Grenade or Nova Bomb leaves behind: a
    /// churning void energy sphere whose visual edge is exactly the damage
    /// radius, ticking AoE damage and dragging enemies toward its heart
    /// until it expires (the tick and pull paths allocate nothing — buffers
    /// are reused). Goes out with a final burst.
    /// </summary>
    public class VortexZone : MonoBehaviour
    {
        private const float TickInterval = 0.33f;
        private const float PullSpeed = 3.5f;

        private static readonly Collider[] pullOverlaps = new Collider[32];
        private static readonly HashSet<CharacterController> pulled = new HashSet<CharacterController>();

        private float damagePerTick;
        private float radius;
        private float remaining;
        private GameObject source;
        private float tickTimer;

        public void Configure(float tickDamage, float zoneRadius, float seconds, GameObject damageSource)
        {
            damagePerTick = tickDamage;
            radius = zoneRadius;
            remaining = seconds;
            source = damageSource;

            // worldPositionStays: the sphere is already at the detonation
            // point; parenting only ties its lifetime to the zone.
            FX.SpawnEnergySphere(transform.position, Element.Void, radius)
                .transform.SetParent(transform, true);
            SFX.AttachLoop(gameObject, SFX.VortexLoopClip, 0.7f, radius * 1.5f);
        }

        private void Update()
        {
            remaining -= Time.deltaTime;

            PullEnemies();

            tickTimer += Time.deltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                AoEDamage.Apply(transform.position, radius, damagePerTick, source);
            }

            if (remaining <= 0f)
            {
                FX.SpawnElementBurst(transform.position, Element.Void, 1.2f);
                Destroy(gameObject);
            }
        }

        /// <summary>The vortex drags anything CharacterController-driven toward
        /// its heart — the caster is exempt; it's their vortex.</summary>
        private void PullEnemies()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, radius, pullOverlaps, ~0,
                QueryTriggerInteraction.Ignore);
            pulled.Clear();
            for (int i = 0; i < n; i++)
            {
                var cc = pullOverlaps[i].GetComponentInParent<CharacterController>();
                if (cc == null || !cc.enabled || !pulled.Add(cc)) continue;
                if (source != null && cc.gameObject == source) continue;

                Vector3 toCenter = transform.position - cc.transform.position;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude < 0.04f) continue; // already at the heart
                cc.Move(toCenter.normalized * (PullSpeed * Time.deltaTime));
            }
        }
    }
}
