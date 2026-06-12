using System.Collections;
using UnityEngine;
using Density3.Core;

namespace Density3.Enemies
{
    /// <summary>
    /// Arc-shielded brute: advances aggressively to close range, throws heavy
    /// three-bolt volleys on the way in, and swipes with its claws inside
    /// arm's reach. The EnergyShield component (same prefab) soaks damage
    /// first; this brain is pure offense.
    /// </summary>
    public class CaptainEnemy : ChaserEnemy
    {
        [Header("Volley")]
        public int volleyBolts = 3;
        public float volleySpacing = 0.18f;

        [Header("Melee")]
        public float meleeRange = 3f;
        public float meleeDamage = 35f;
        public float meleeInterval = 1.6f;

        private float nextMelee;
        private WaitForSeconds volleyWait;

        /// <summary>Canonical Captain tuning — shared by the bootstrap bake and
        /// the runtime fallback (ClassKits.Configure pattern).</summary>
        public static void Configure(EnemyData d)
        {
            d.displayName = "Captain";
            d.maxHealth = 420f;
            d.moveSpeed = 5.2f;     // advances aggressively
            d.strafeSpeed = 2.5f;
            d.aggroRange = 55f;
            d.preferredRange = 7f;  // wants to be in your face
            d.fireRange = 32f;
            d.fireInterval = 3.2f;
            d.projectileDamage = 13f; // per bolt; a full volley is 39
            d.projectileSpeed = 20f;
        }

        protected override EnemyData DefaultData()
        {
            var d = base.DefaultData();
            Configure(d);
            return d;
        }

        protected override void Fire()
        {
            if (volleyWait == null) volleyWait = new WaitForSeconds(volleySpacing);
            StartCoroutine(Volley());
        }

        private IEnumerator Volley()
        {
            for (int i = 0; i < volleyBolts; i++)
            {
                if (health.IsDead || player == null || playerHealth == null || playerHealth.IsDead)
                    yield break;
                base.Fire(); // single aimed bolt + sound + animator recoil
                yield return volleyWait;
            }
        }

        protected override void Tick(Vector3 fwd, float dist)
        {
            if (dist > meleeRange || Time.time < nextMelee) return;
            nextMelee = Time.time + meleeInterval;
            Swipe();
        }

        /// <summary>Claw swipe: direct contact damage with a whoosh-then-crunch
        /// beat. RaiseFired drives the animator's arm kick as the swing.</summary>
        private void Swipe()
        {
            if (playerHealth == null || playerHealth.IsDead) return;

            Vector3 at = player.position + Vector3.up * 0.5f;
            RaiseFired();
            SFX.Play3D(SFX.AbilityMeleeClip, transform.position, 0.9f, 6f, 0.85f);
            FX.SpawnElementBurst(at, Element.Arc, 0.6f);
            SFX.Play3D(SFX.MeleeImpactClip, at, 0.9f, 6f, 0.9f);
            playerHealth.ApplyDamage(new DamageInfo
            {
                amount = meleeDamage,
                isCrit = false,
                hitPoint = at,
                source = gameObject
            });
        }
    }
}
