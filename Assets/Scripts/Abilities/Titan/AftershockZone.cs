using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The ground left angry after a Fists of Havoc slam: light arc damage
    /// ticking in the slam zone for a few seconds, with crackling bursts
    /// scattered across the area so the danger reads. The tick path
    /// allocates nothing.
    /// </summary>
    public class AftershockZone : MonoBehaviour
    {
        private const float TickInterval = 0.5f;
        private const float CrackleInterval = 0.35f;

        private float damagePerTick;
        private float radius;
        private float remaining;
        private GameObject source;
        private float tickTimer;
        private float crackleTimer;

        public void Configure(float tickDamage, float zoneRadius, float seconds, GameObject damageSource)
        {
            damagePerTick = tickDamage;
            radius = zoneRadius;
            remaining = seconds;
            source = damageSource;

            var glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = ElementPalette.Base(Element.Arc);
            glow.range = radius * 2f;
            glow.intensity = 1.8f;

            SFX.AttachLoop(gameObject, SFX.ArcLoopClip, 0.45f, radius);
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

            crackleTimer += Time.deltaTime;
            if (crackleTimer >= CrackleInterval)
            {
                crackleTimer -= CrackleInterval;
                Vector2 spot = Random.insideUnitCircle * (radius * 0.7f);
                FX.SpawnElementBurst(
                    transform.position + new Vector3(spot.x, 0.15f, spot.y), Element.Arc, 0.45f);
            }

            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
