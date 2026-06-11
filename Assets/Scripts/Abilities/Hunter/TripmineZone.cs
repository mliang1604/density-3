using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// An armed tripmine, living on the stuck projectile: after a short arm
    /// delay it projects a solar laser along the surface normal (clamped to
    /// the first wall) and detonates when anything with Health crosses the
    /// beam. It also carries a 1hp Health + Hitbox of its own, so shooting
    /// the mine sets it off.
    /// </summary>
    public class TripmineZone : MonoBehaviour
    {
        private const float ArmDelay = 0.6f;

        private float damage;
        private float radius;
        private float beamLength;
        private Vector3 beamDir;
        private GameObject source;
        private float armTimer;
        private bool armed;
        private bool detonated;
        private Health health;

        public void Configure(Vector3 normal, float blastDamage, float blastRadius,
            float length, GameObject damageSource)
        {
            beamDir = normal;
            damage = blastDamage;
            radius = blastRadius;
            beamLength = length;
            source = damageSource;

            // Shootable: a tiny health pool routed through the regular Hitbox path.
            health = gameObject.AddComponent<Health>();
            health.SetMaxHealth(1f);
            var collider = gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.22f;
            var hb = gameObject.AddComponent<Hitbox>();
            hb.owner = health;
            health.Died += OnShot;
        }

        private void OnShot() => Detonate();

        private void Update()
        {
            if (detonated) return;

            if (!armed)
            {
                armTimer += Time.deltaTime;
                if (armTimer < ArmDelay) return;
                armed = true;

                // Clamp the beam to the first obstacle so it can't trigger
                // (or draw) through walls.
                if (Physics.Raycast(transform.position, beamDir, out RaycastHit block,
                        beamLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Health>() == null)
                    beamLength = block.distance;

                var beam = FX.SpawnBeam(transform.position,
                    transform.position + beamDir * beamLength, Element.Solar, 0.03f);
                beam.transform.SetParent(transform, true); // dies with the mine
                SFX.Play3D(SFX.AbilityReadyClip, transform.position, 0.4f, 4f);
                return;
            }

            if (Physics.Raycast(transform.position, beamDir, out RaycastHit hit,
                    beamLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var crosser = hit.collider.GetComponentInParent<Health>();
                if (crosser != null && crosser.gameObject != source && crosser != health)
                    Detonate();
            }
        }

        private void Detonate()
        {
            if (detonated) return;
            detonated = true;
            AoEDamage.Apply(transform.position, radius, damage, source);
            FX.SpawnElementBurst(transform.position, Element.Solar, 1.4f);
            SFX.Play3D(SFX.AbilityDetonateClip, transform.position, 0.9f, 8f);
            Destroy(gameObject);
        }
    }
}
