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
        [Tooltip("Optional in-flight steering toward homingTarget, degrees/second. 0 = ballistic.")]
        public float homingDegreesPerSecond;
        public Transform homingTarget;
        [Tooltip("Offset added to the homing target's position. Default 1m up = center mass; zero when homing at a precise transform like a crit zone.")]
        public Vector3 homingAimOffset = Vector3.up;
        [Tooltip("Spherecast radius for contact checks. 0 = thin ray.")]
        public float castRadius;

        /// <summary>Raised with the full RaycastHit on any surface contact,
        /// before stick/detonate handling — payloads that need the collider
        /// (Hitbox routing, crits) subscribe here.</summary>
        public event Action<RaycastHit> Impacted;

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

            if (homingTarget != null && homingDegreesPerSecond > 0f)
            {
                Vector3 toTarget = homingTarget.position + homingAimOffset - transform.position;
                vel = Vector3.RotateTowards(vel, toTarget.normalized * vel.magnitude,
                    homingDegreesPerSecond * Mathf.Deg2Rad * Time.deltaTime, 0f);
            }

            vel.y += gravity * Time.deltaTime;
            float step = vel.magnitude * Time.deltaTime;
            RaycastHit hit;
            bool contact = castRadius > 0f
                ? Physics.SphereCast(transform.position, castRadius, vel.normalized, out hit,
                    step + 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : Physics.Raycast(transform.position, vel.normalized, out hit, step + 0.03f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (contact)
            {
                Impacted?.Invoke(hit);
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

            // Release any trail into the world so it fades out over its own
            // time instead of vanishing with the projectile.
            var trail = GetComponentInChildren<TrailRenderer>();
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time);
            }
            Destroy(gameObject);
        }
    }
}
