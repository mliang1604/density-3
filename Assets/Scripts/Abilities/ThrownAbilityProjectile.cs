using System;
using UnityEngine;

namespace Density3.Abilities
{
    /// <summary>
    /// Gravity-arc projectile for thrown abilities (grenades, knives) —
    /// the ballistic cousin of the straight-flying EnemyProjectile. Steps
    /// with raycasts so fast throws can't tunnel through walls; the thrower
    /// is skipped for free because the player sits on the Ignore Raycast
    /// layer. Sticks to the first surface or detonates on it, per flags;
    /// the fuse counts from launch. The owning ability supplies the
    /// payload via the Stuck/Detonated events.
    /// </summary>
    public class ThrownAbilityProjectile : MonoBehaviour
    {
        public float gravity = -18f;
        [Tooltip("Seconds from launch to forced detonation. 0 = impact/stick only.")]
        public float fuseSeconds = 2f;
        public bool stickToSurface;
        public bool detonateOnImpact = true;

        /// <summary>Hit point and surface normal, when stickToSurface lands.</summary>
        public event Action<Vector3, Vector3> Stuck;

        /// <summary>Where the payload goes off. The projectile destroys itself after.</summary>
        public event Action<Vector3> Detonated;

        private Vector3 vel;
        private bool live;
        private bool stuck;
        private float fuse;

        public void Launch(Vector3 velocity)
        {
            vel = velocity;
            fuse = fuseSeconds;
            live = true;
        }

        private void Update()
        {
            if (!live) return;

            if (fuseSeconds > 0f)
            {
                fuse -= Time.deltaTime;
                if (fuse <= 0f)
                {
                    Detonate(transform.position);
                    return;
                }
            }
            if (stuck) return;

            vel.y += gravity * Time.deltaTime;
            float step = vel.magnitude * Time.deltaTime;
            if (Physics.Raycast(transform.position, vel.normalized, out RaycastHit hit, step + 0.03f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (stickToSurface)
                {
                    transform.position = hit.point + hit.normal * 0.02f;
                    stuck = true;
                    Stuck?.Invoke(hit.point, hit.normal);
                }
                else if (detonateOnImpact)
                {
                    Detonate(hit.point + hit.normal * 0.05f);
                }
                else
                {
                    // Come to rest and wait out the fuse.
                    transform.position = hit.point + hit.normal * 0.05f;
                    vel = Vector3.zero;
                }
                return;
            }
            transform.position += vel * Time.deltaTime;
        }

        private void Detonate(Vector3 at)
        {
            live = false;
            Detonated?.Invoke(at);
            Destroy(gameObject);
        }
    }
}
