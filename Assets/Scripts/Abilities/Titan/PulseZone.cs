using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The pulse grenade's detonation zone: a churning arc energy sphere
    /// that grows with each of three shocks at a fixed cadence — the sphere's
    /// edge always shows the kill radius, swelling to full size on the final
    /// pulse. Each shock damages the zone, bursts, and starts an arc chain
    /// from the victim nearest the heart. Self-destructs after the last pulse.
    /// </summary>
    public class PulseZone : MonoBehaviour
    {
        private const int PulseCount = 3;
        private const float PulseInterval = 0.9f;
        private const float StartScale = 0.35f;
        private const float GrowSpeed = 1.6f; // scale units/sec toward each pulse's size

        private float damagePerPulse;
        private float radius;
        private float chainDamage;
        private GameObject source;
        private float timer;
        private int pulsesDone;
        private Transform sphere;
        private float targetScale = StartScale;

        public void Configure(float pulseDamage, float pulseRadius, float chainDamagePerJump,
            GameObject damageSource)
        {
            damagePerPulse = pulseDamage;
            radius = pulseRadius;
            chainDamage = chainDamagePerJump;
            source = damageSource;

            // Built at full radius, scaled down, grown pulse by pulse — the
            // shell's edge is the damage edge at the final size.
            var energySphere = FX.SpawnEnergySphere(transform.position, Element.Arc, radius);
            energySphere.transform.SetParent(transform, true); // worldPositionStays
            sphere = energySphere.transform;
            sphere.localScale = Vector3.one * StartScale;

            timer = PulseInterval; // first pulse fires immediately
        }

        private void Update()
        {
            if (sphere != null)
                sphere.localScale = Vector3.one * Mathf.MoveTowards(
                    sphere.localScale.x, targetScale, GrowSpeed * Time.deltaTime);

            if (pulsesDone >= PulseCount) return;

            timer += Time.deltaTime;
            if (timer < PulseInterval) return;
            timer = 0f;

            pulsesDone++;
            targetScale = (float)pulsesDone / PulseCount;
            AoEDamage.Apply(transform.position, radius, damagePerPulse, source);
            FX.SpawnElementBurst(transform.position, Element.Arc, 0.9f + 0.35f * pulsesDone);
            SFX.Play3D(SFX.BoltImpactClip, transform.position, 0.8f, 8f);

            var chainOrigin = ChainLightning.NearestTarget(transform.position, radius, source);
            if (chainOrigin != null) ChainLightning.Chain(chainOrigin, chainDamage, source);

            if (pulsesDone >= PulseCount) Destroy(gameObject, 0.4f); // let the sphere finish growing
        }
    }
}
