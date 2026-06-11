using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Warlock grenade: a thrown void charge that detonates on impact (or a
    /// short fuse) and leaves a lingering vortex ticking AoE damage. The
    /// detonation pop softens targets; the vortex does the real work on
    /// anything that stays inside.
    /// </summary>
    public class VortexGrenadeAbility : AbilityBase
    {
        public float throwSpeed = 28f; // fast and flat enough to aim directly
        public float detonateDamage = 50f;
        public float vortexDamagePerTick = 12f;
        public float vortexRadius = 3f;
        public float vortexSeconds = 6f;

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

            var go = FX.SpawnBolt(cam.position + cam.forward * 0.5f, Element.Void);
            FX.AddElementTrail(go, Element.Void);
            var proj = go.AddComponent<ThrownAbilityProjectile>();
            proj.fuseSeconds = 2.5f;
            proj.detonateOnImpact = true;
            proj.Detonated += at =>
            {
                AoEDamage.Apply(at, vortexRadius, detonateDamage, gameObject);
                FX.SpawnElementBurst(at, Element.Void, 1.2f);
                var zone = new GameObject("VortexZone").AddComponent<VortexZone>();
                zone.transform.position = at;
                zone.Configure(vortexDamagePerTick, vortexRadius, vortexSeconds, gameObject);
            };
            proj.Launch(cam.forward * throwSpeed + Vector3.up * 1.5f);
            SFX.Play2D(SFX.AbilityThrowClip, 0.6f);
        }
    }
}
