using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The pulse grenade's detonation zone: three arc shocks at a fixed
    /// cadence, each with an expanding burst so the pulses read as distinct
    /// hits (and produce three distinct damage numbers). Self-destructs
    /// after the last pulse.
    /// </summary>
    public class PulseZone : MonoBehaviour
    {
        private const int PulseCount = 3;
        private const float PulseInterval = 0.9f;

        private float damagePerPulse;
        private float radius;
        private GameObject source;
        private float timer;
        private int pulsesDone;
        private Light glow;

        public void Configure(float pulseDamage, float pulseRadius, GameObject damageSource)
        {
            damagePerPulse = pulseDamage;
            radius = pulseRadius;
            source = damageSource;

            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = ElementPalette.Base(Element.Arc);
            glow.range = radius * 2f;
            glow.intensity = 2.5f;

            timer = PulseInterval; // first pulse fires immediately
        }

        private void Update()
        {
            // The glow crackles between pulses.
            if (glow != null)
                glow.intensity = 1.5f + Mathf.PingPong(Time.time * 9f, 1.5f);

            timer += Time.deltaTime;
            if (timer < PulseInterval) return;
            timer = 0f;

            pulsesDone++;
            AoEDamage.Apply(transform.position, radius, damagePerPulse, source);
            FX.SpawnElementBurst(transform.position, Element.Arc, 0.9f + 0.35f * pulsesDone);
            SFX.Play3D(SFX.BoltImpactClip, transform.position, 0.8f, 8f);

            if (pulsesDone >= PulseCount) Destroy(gameObject);
        }
    }
}
