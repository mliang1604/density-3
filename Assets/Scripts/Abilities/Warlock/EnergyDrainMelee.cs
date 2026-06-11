using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Warlock melee: a short-range void palm. Lands through the regular
    /// Hitbox path so crit zones still count. The drain identity: any hit
    /// refunds a chunk of grenade energy, and a kill refunds the melee in
    /// full. Spam is gated by the ability's own charge — activation always
    /// spends the bar.
    /// </summary>
    public class EnergyDrainMelee : AbilityBase
    {
        public float range = 3f;
        public float castRadius = 0.6f; // forgiving palm-sized spherecast
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

            if (!Physics.SphereCast(cam.position, castRadius, cam.forward, out RaycastHit hit,
                    range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

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
