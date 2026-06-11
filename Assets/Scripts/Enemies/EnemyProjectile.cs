using UnityEngine;
using FableFPS.Core;

namespace FableFPS.Enemies
{
    /// <summary>Straight-flying energy bolt. Damages the player on proximity.</summary>
    public class EnemyProjectile : MonoBehaviour
    {
        private float speed = 16f;
        private float damage = 12f;
        private float lifetime = 6f;
        private Vector3 dir;
        private Health target;

        public void Launch(Vector3 direction, Health targetHealth, float projectileSpeed, float projectileDamage)
        {
            dir = direction.normalized;
            target = targetHealth;
            speed = projectileSpeed;
            damage = projectileDamage;
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;

            // World geometry check (player is on Ignore Raycast, handled below by proximity).
            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, step + 0.05f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                FX.SpawnImpact(hit.point, hit.normal);
                SFX.Play3D(SFX.BoltImpactClip, hit.point, 0.6f);
                Destroy(gameObject);
                return;
            }
            transform.position += dir * step;

            if (target != null && !target.IsDead)
            {
                Vector3 chest = target.transform.position + Vector3.up * 0.5f;
                if ((transform.position - chest).sqrMagnitude < 1.1f)
                {
                    target.ApplyDamage(new DamageInfo
                    {
                        amount = damage,
                        isCrit = false,
                        hitPoint = transform.position,
                        source = gameObject
                    });
                    Destroy(gameObject);
                    return;
                }
            }

            lifetime -= Time.deltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }
    }
}
