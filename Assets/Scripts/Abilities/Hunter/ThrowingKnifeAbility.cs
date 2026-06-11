using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Hunter melee: a fast, slightly arcing throwing knife. Unlike the
    /// Warlock palm it has no tracking and it crits — the precision identity:
    /// headshots one-shot a Dreg, and a precision kill refunds the melee in
    /// full (Knife Juggler). Body hits do reduced damage and refund nothing.
    /// </summary>
    public class ThrowingKnifeAbility : AbilityBase
    {
        public float throwSpeed = 35f;
        public float damage = 60f;
        public float critMultiplier = 3f; // 180 on the head: lethal to a Dreg
        public float maxFlightSeconds = 2f;
        public float castRadius = 0.15f; // a knife rewards aim

        private PlayerController player;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
        }

        protected override void OnActivate()
        {
            SFX.Play2D(SFX.AbilityThrowClip, 0.5f, 1.2f);
            Transform cam = player != null && player.playerCamera != null
                ? player.playerCamera.transform : transform;

            var knife = FX.SpawnBolt(cam.position + cam.forward * 0.4f, Element.Solar);
            knife.name = "ThrowingKnife";
            knife.transform.localScale = new Vector3(0.08f, 0.08f, 0.3f);
            knife.transform.rotation = Quaternion.LookRotation(cam.forward);
            FX.AddElementTrail(knife, Element.Solar, 0.15f);

            var proj = knife.AddComponent<ThrownAbilityProjectile>();
            proj.gravity = -8f; // slight drop at range
            proj.detonateOnImpact = true;
            proj.fuseSeconds = maxFlightSeconds;
            proj.castRadius = castRadius;
            proj.Impacted += OnKnifeHit;
            proj.Launch(cam.forward * throwSpeed);
        }

        private void OnKnifeHit(RaycastHit hit)
        {
            var hb = hit.collider.GetComponent<Hitbox>();
            if (hb == null)
            {
                FX.SpawnElementBurst(hit.point, Element.Solar, 0.3f);
                return;
            }

            var targetHealth = hb.owner;
            bool wasAlive = targetHealth != null && !targetHealth.IsDead;
            float applied = hb.Hit(damage, critMultiplier, hit.point, gameObject);
            if (applied <= 0f) return;

            DamageNumbers.Spawn(hit.point, applied, hb.isCritZone);
            FX.SpawnElementBurst(hit.point, Element.Solar, hb.isCritZone ? 0.7f : 0.45f);
            SFX.Play3D(SFX.MeleeImpactClip, hit.point, 0.7f, 6f);

            // Knife Juggler: only the precision kill brings the knife back.
            if (wasAlive && targetHealth.IsDead && hb.isCritZone) AddEnergy(1f);
        }
    }
}
