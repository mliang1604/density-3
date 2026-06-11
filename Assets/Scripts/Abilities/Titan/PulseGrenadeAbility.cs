using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Titan grenade: an arc charge that detonates on first impact, then
    /// pulses two more times — three expanding shocks over ~2s that
    /// together kill anything Dreg-sized that stays in the zone.
    /// </summary>
    public class PulseGrenadeAbility : AbilityBase
    {
        public float throwSpeed = 28f;
        public float damagePerPulse = 60f; // x3 = 180 for the full stay
        public float pulseRadius = 4f;

        private PlayerController player;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
        }

        protected override void OnActivate()
        {
            Transform cam = player != null && player.playerCamera != null
                ? player.playerCamera.transform : transform;

            var go = FX.SpawnBolt(cam.position + cam.forward * 0.5f, Element.Arc);
            FX.AddElementTrail(go, Element.Arc);
            var proj = go.AddComponent<ThrownAbilityProjectile>();
            proj.fuseSeconds = 2.5f;
            proj.detonateOnImpact = true;
            proj.Detonated += at =>
            {
                var zone = new GameObject("PulseZone").AddComponent<PulseZone>();
                zone.transform.position = at;
                zone.Configure(damagePerPulse, pulseRadius, gameObject);
            };
            proj.Launch(cam.forward * throwSpeed + Vector3.up * 1.5f);
            SFX.Play2D(SFX.AbilityThrowClip, 0.6f);
        }
    }
}
