using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Routes ability input to the four ability slots. Binds follow the
    /// project's serialized-KeyCode convention; G/V/F/Q are free keys that
    /// browsers don't claim in WebGL. Slots are populated by ClassLoadout at
    /// spawn — empty slots no-op, so the router is safe on any player. When
    /// the charged melee isn't ready, the melee key falls back to a basic
    /// uncharged punch on a short rate limit.
    /// </summary>
    public class PlayerAbilities : MonoBehaviour
    {
        [Header("Binds")]
        public KeyCode grenadeKey = KeyCode.G;
        public KeyCode meleeKey = KeyCode.V;
        public KeyCode classAbilityKey = KeyCode.F;
        public KeyCode superKey = KeyCode.Q;

        [Header("Uncharged melee")]
        public float basicMeleeDamage = 35f;
        public float basicMeleeRange = 2.5f;
        public float basicMeleeRadius = 0.8f;
        public float basicMeleeCooldown = 1f;

        [Header("Slots (populated by ClassLoadout)")]
        public AbilityBase grenade;
        public AbilityBase melee;
        public AbilityBase classAbility;
        public AbilityBase super;

        private PlayerController controller;
        private Health health;
        private float nextBasicMelee;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (health != null && health.IsDead) return;
            if (controller != null && controller.MovementLocked) return;

            if (Input.GetKeyDown(grenadeKey)) Activate(grenade);
            if (Input.GetKeyDown(meleeKey) && (melee == null || !melee.TryActivate()))
                BasicMelee();
            if (Input.GetKeyDown(classAbilityKey)) Activate(classAbility);
            if (Input.GetKeyDown(superKey)) Activate(super);
        }

        private static void Activate(AbilityBase ability)
        {
            if (ability != null) ability.TryActivate();
        }

        /// <summary>Generic uncharged punch — class-agnostic, no element, no
        /// refunds. Keeps V useful while the charged melee recharges.</summary>
        private void BasicMelee()
        {
            if (Time.time < nextBasicMelee) return;
            nextBasicMelee = Time.time + basicMeleeCooldown;
            SFX.Play2D(SFX.AbilityMeleeClip, 0.45f, 1.3f);

            Transform cam = controller != null && controller.playerCamera != null
                ? controller.playerCamera.transform : transform;
            if (!Physics.SphereCast(cam.position, basicMeleeRadius, cam.forward, out RaycastHit hit,
                    basicMeleeRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return;

            var hb = hit.collider.GetComponent<Hitbox>();
            if (hb != null)
            {
                float applied = hb.Hit(basicMeleeDamage, 1f, hit.point, gameObject);
                if (applied > 0f) DamageNumbers.Spawn(hit.point, applied, false);
            }
            FX.SpawnImpact(hit.point, hit.normal);
        }
    }
}
