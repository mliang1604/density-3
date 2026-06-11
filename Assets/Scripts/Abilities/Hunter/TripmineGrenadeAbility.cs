using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Hunter grenade: a thrown solar charge that sticks to the first
    /// surface and arms a laser tripwire along the surface normal. Crossing
    /// the beam or shooting the mine detonates it; an unstuck or untriggered
    /// mine fizzles out with the projectile's 30s fuse.
    /// </summary>
    public class TripmineGrenadeAbility : AbilityBase
    {
        public float throwSpeed = 24f;
        public float damage = 325f;
        public float blastRadius = 3.5f;
        public float beamLength = 4f;
        public float lifetimeSeconds = 30f;

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

            var go = FX.SpawnBolt(cam.position + cam.forward * 0.5f, Element.Solar);
            go.name = "Tripmine";
            go.transform.localScale = Vector3.one * 0.3f;
            FX.AddElementTrail(go, Element.Solar, 0.2f);

            var proj = go.AddComponent<ThrownAbilityProjectile>();
            proj.gravity = -14f;
            proj.stickToSurface = true;
            proj.detonateOnImpact = false;
            proj.fuseSeconds = lifetimeSeconds; // unstuck or ignored mines fizzle
            proj.Stuck += (point, normal) =>
            {
                var zone = go.AddComponent<TripmineZone>();
                zone.Configure(normal, damage, blastRadius, beamLength, gameObject);
            };
            proj.Launch(cam.forward * throwSpeed + Vector3.up * 1.2f);
            SFX.Play2D(SFX.AbilityThrowClip, 0.6f);
        }
    }
}
