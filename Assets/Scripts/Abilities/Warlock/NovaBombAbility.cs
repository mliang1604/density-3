using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Warlock super: fires instantly — a large, slow void bomb that seeks
    /// the target nearest the throw vector and detonates on impact with a
    /// huge AoE, leaving a suction vortex. Screen kick and a void-tinted
    /// vignette pulse sell the weight. Energy is the full super bar, spent
    /// by the activation gate.
    /// </summary>
    public class NovaBombAbility : AbilityBase
    {
        public float projectileSpeed = 14f;
        public float bombCastRadius = 1.2f; // fat contact check — near misses connect
        public float damage = 400f;
        public float blastRadius = 14.4f;
        public float vortexRadius = 9.6f;
        public float vortexDamagePerTick = 25f;
        public float vortexSeconds = 6f;
        [Tooltip("Only enemies within this angle of the throw vector are tracked.")]
        public float trackingConeDegrees = 35f;
        public float trackingTurnRate = 180f; // deg/sec — seeks hard

        private PlayerController player;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
        }

        protected override void OnActivate()
        {
            SFX.Play2D(SFX.SuperActivateClip, 0.9f);

            Transform cam = player != null && player.playerCamera != null
                ? player.playerCamera.transform : transform;

            var go = FX.SpawnBolt(cam.position + cam.forward * 0.8f, Element.Void);
            go.name = "NovaBomb";
            go.transform.localScale = Vector3.one * 0.6f;
            FX.AddElementTrail(go, Element.Void, 0.6f);
            var bombLight = go.AddComponent<Light>();
            bombLight.type = LightType.Point;
            bombLight.color = ElementPalette.Base(Element.Void);
            bombLight.range = 6f;
            bombLight.intensity = 3f;

            var proj = go.AddComponent<ThrownAbilityProjectile>();
            proj.gravity = -2f; // heavy float, mostly straight
            proj.fuseSeconds = 6f;
            proj.detonateOnImpact = true;
            proj.castRadius = bombCastRadius;
            proj.homingTarget = Targeting.NearestToAim(
                cam.position, cam.forward, trackingConeDegrees, 0f, gameObject);
            proj.homingDegreesPerSecond = trackingTurnRate;
            proj.Detonated += Detonate;
            proj.Launch(cam.forward * projectileSpeed);

            if (player != null) player.AddRecoil(6f, 1.5f);
            var hud = FindFirstObjectByType<HUDController>();
            if (hud != null) hud.PulseVignette(ElementPalette.Base(Element.Void), 0.45f);
        }

        private void Detonate(Vector3 at)
        {
            AoEDamage.Apply(at, blastRadius, damage, gameObject);
            FX.SpawnElementBurst(at, Element.Void, 2.5f);
            // Deep 2D crack plus a positioned thump — the boom is borrowed
            // from the heavy gunshot, pitched down.
            SFX.Play2D(SFX.GunshotFor(100f), 0.9f, 0.6f);
            SFX.Play3D(SFX.BoltImpactClip, at, 1f, 12f);

            // Vortex Nova: the blast leaves a churning vortex behind, which
            // also demarks the danger radius on the ground.
            var zone = new GameObject("NovaVortex").AddComponent<VortexZone>();
            zone.transform.position = at;
            zone.Configure(vortexDamagePerTick, vortexRadius, vortexSeconds, gameObject);
        }
    }
}
