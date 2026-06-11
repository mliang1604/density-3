using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Warlock melee: a homing void wave. The wave IS the attack — it
    /// carries the damage and curves toward the target nearest the aim
    /// vector, so the visual and the hit can never desync. Lands through
    /// the regular Hitbox path so crit zones still count. The drain
    /// identity: any hit refunds a chunk of grenade energy, and a kill
    /// refunds the melee in full. Spam is gated by the activation cost.
    /// </summary>
    public class EnergyDrainMelee : AbilityBase
    {
        public float range = 11f;             // a true void lunge
        public float trackingConeDegrees = 35f;
        public float trackingTurnRate = 240f; // deg/sec — the palm snaps hard
        public float waveSpeed = 30f;
        public float waveCastRadius = 0.5f;   // forgiving contact check
        public float damage = 80f;
        public float critMultiplier = 1.4f;
        [Range(0f, 1f)] public float grenadeRefundOnHit = 0.25f;

        private PlayerController player;
        private PlayerAbilities slots;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
            slots = GetComponent<PlayerAbilities>();
        }

        protected override void OnActivate()
        {
            SFX.Play2D(SFX.AbilityMeleeClip, 0.7f);
            Transform cam = player != null && player.playerCamera != null
                ? player.playerCamera.transform : transform;

            var wave = FX.SpawnBolt(cam.position + cam.forward * 0.4f, Element.Void);
            wave.name = "VoidPalmWave";
            wave.transform.localScale = Vector3.one * 0.45f;
            FX.AddElementTrail(wave, Element.Void, 0.35f);
            var waveLight = wave.AddComponent<Light>();
            waveLight.type = LightType.Point;
            waveLight.color = ElementPalette.Base(Element.Void);
            waveLight.range = 4f;
            waveLight.intensity = 2.5f;

            var waveProj = wave.AddComponent<ThrownAbilityProjectile>();
            waveProj.gravity = 0f;
            waveProj.detonateOnImpact = true;
            waveProj.fuseSeconds = range / waveSpeed;
            waveProj.castRadius = waveCastRadius;
            waveProj.homingTarget = Targeting.NearestToAim(
                cam.position, cam.forward, trackingConeDegrees, range * 1.2f, gameObject);
            waveProj.homingDegreesPerSecond = trackingTurnRate;
            waveProj.Impacted += OnWaveImpact;
            waveProj.Launch(cam.forward * waveSpeed);
        }

        private void OnWaveImpact(RaycastHit hit)
        {
            var hb = hit.collider.GetComponent<Hitbox>();
            if (hb == null)
            {
                FX.SpawnElementBurst(hit.point, Element.Void, 0.4f);
                return;
            }

            var targetHealth = hb.owner;
            bool wasAlive = targetHealth != null && !targetHealth.IsDead;
            float applied = hb.Hit(damage, critMultiplier, hit.point, gameObject);
            if (applied <= 0f) return;

            DamageNumbers.Spawn(hit.point, applied, hb.isCritZone);
            FX.SpawnElementBurst(hit.point, Element.Void, 0.6f);

            // The drain: hits feed the grenade, kills refund the melee in full.
            if (slots != null && slots.grenade != null) slots.grenade.AddEnergy(grenadeRefundOnHit);
            if (wasAlive && targetHealth.IsDead) AddEnergy(1f);
        }
    }
}
