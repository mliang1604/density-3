using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The pulse grenade's detonation zone: a churning arc energy sphere
    /// that breathes with the shocks. Each pulse pops the sphere out to a
    /// bigger size (half, three-quarters, then full radius) and damages
    /// exactly that area; between shocks the sphere contracts as it charges
    /// the next one. Every shock also starts an arc chain from the victim
    /// nearest the heart. Self-destructs after the third pulse.
    /// </summary>
    public class PulseZone : MonoBehaviour
    {
        private const float PulseInterval = 0.9f;
        private const float ContractFraction = 0.78f; // how far it shrinks between shocks
        private static readonly float[] PulseScales = { 0.5f, 0.75f, 1f };

        private float damagePerPulse;
        private float radius;
        private float chainDamage;
        private GameObject source;
        private float timer;
        private int pulsesDone;
        private Transform sphere;
        private float currentBase = 0.35f;

        public void Configure(float pulseDamage, float pulseRadius, float chainDamagePerJump,
            GameObject damageSource)
        {
            damagePerPulse = pulseDamage;
            radius = pulseRadius;
            chainDamage = chainDamagePerJump;
            source = damageSource;

            // Built at full radius and scaled, so the shell's edge always
            // shows exactly where the current pulse hurts.
            var energySphere = FX.SpawnEnergySphere(transform.position, Element.Arc, radius);
            energySphere.transform.SetParent(transform, true); // worldPositionStays
            sphere = energySphere.transform;
            sphere.localScale = Vector3.one * currentBase;
            SFX.AttachLoop(gameObject, SFX.ArcLoopClip, 0.5f, radius);

            timer = PulseInterval; // first pulse fires immediately
        }

        private void Update()
        {
            timer += Time.deltaTime;

            // The breath: pop to the pulse size on the shock, contract while
            // charging the next.
            if (sphere != null)
            {
                float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / PulseInterval));
                sphere.localScale = Vector3.one
                    * Mathf.Lerp(currentBase, currentBase * ContractFraction, settle);
            }

            if (pulsesDone >= PulseScales.Length || timer < PulseInterval) return;
            timer = 0f;

            currentBase = PulseScales[pulsesDone];
            pulsesDone++;
            float pulseRadiusNow = radius * currentBase;

            AoEDamage.Apply(transform.position, pulseRadiusNow, damagePerPulse, source);
            FX.SpawnElementBurst(transform.position, Element.Arc, 0.7f + currentBase);
            SFX.Play3D(SFX.ArcShockClip, transform.position, 0.85f, 8f);

            var chainOrigin = ChainLightning.NearestTarget(transform.position, pulseRadiusNow, source);
            if (chainOrigin != null) ChainLightning.Chain(chainOrigin, chainDamage, source);

            if (pulsesDone >= PulseScales.Length)
                Destroy(gameObject, 0.5f); // one last exhale, then gone
        }
    }
}
