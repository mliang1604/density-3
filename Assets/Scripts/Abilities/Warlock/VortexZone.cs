using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The lingering vortex a Vortex Grenade leaves behind: ticks AoE damage
    /// at a fixed rate (the tick path allocates nothing — AoEDamage reuses
    /// its buffers) and re-triggers a small void burst every half second so
    /// the zone reads as boiling gas. Self-destructs when the time is up.
    /// </summary>
    public class VortexZone : MonoBehaviour
    {
        private const float TickInterval = 0.33f;
        private const float PuffInterval = 0.5f;

        private float damagePerTick;
        private float radius;
        private float remaining;
        private GameObject source;
        private float tickTimer;
        private float puffTimer;

        public void Configure(float tickDamage, float zoneRadius, float seconds, GameObject damageSource)
        {
            damagePerTick = tickDamage;
            radius = zoneRadius;
            remaining = seconds;
            source = damageSource;

            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ElementPalette.Base(Element.Void);
            light.range = radius * 2.5f;
            light.intensity = 2.2f;
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

            puffTimer += Time.deltaTime;
            if (puffTimer >= PuffInterval)
            {
                puffTimer -= PuffInterval;
                FX.SpawnElementBurst(transform.position, Element.Void, 0.8f);
            }

            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
