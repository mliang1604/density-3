using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The lingering vortex a Vortex Grenade or Nova Bomb leaves behind: a
    /// churning void energy sphere whose visual edge is exactly the damage
    /// radius, ticking AoE damage until it expires (the tick path allocates
    /// nothing — AoEDamage reuses its buffers). Goes out with a final burst.
    /// </summary>
    public class VortexZone : MonoBehaviour
    {
        private const float TickInterval = 0.33f;

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
        }

        private void Update()
        {
            remaining -= Time.deltaTime;

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
    }
}
