using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.Abilities
{
    /// <summary>
    /// Hunter super: three shots of a massive-damage golden hand cannon,
    /// riding the existing HandCannon machinery via its weapon-override API
    /// with a dedicated runtime-built golden viewmodel (the frame's model
    /// hides for the duration). Ends after three shots or the timer; the
    /// previous weapon returns with its exact mag.
    /// </summary>
    public class GoldenGunAbility : AbilityBase
    {
        public float durationSeconds = 12f;
        public int rounds = 3;
        public float explosionDamage = 160f;
        public float explosionRadius = 4f;
        public float chainDelaySeconds = 0.15f; // the chain reads as a cascade, not one blast
        public float shotTrailSeconds = 1.6f;   // the shot line hangs in the air
        [Range(0f, 1f)] public float superKillRefund = 0.05f; // bonus super energy per kill while drawn

        private HandCannon weapon;
        private PlayerController player;
        private WeaponData goldenData;
        private WeaponViewmodel goldenViewmodel;
        private float endTime;
        private bool active;

        protected override void Awake()
        {
            base.Awake();
            weapon = GetComponent<HandCannon>();
            player = GetComponent<PlayerController>();
        }

        protected override void OnActivate()
        {
            if (weapon == null) return;
            if (goldenData == null) goldenData = BuildGoldenData();

            if (weapon.viewmodel != null)
                goldenViewmodel = GoldenGunViewmodel.Build(weapon.viewmodel);
            weapon.BeginOverride(goldenData, rounds, goldenViewmodel);
            weapon.TargetKilled += OnGunKill;
            weapon.ShotFired += OnGoldenShot;
            GameEvents.EnemyKilled += OnSuperKill;
            active = true;
            endTime = Time.time + durationSeconds;

            SFX.Play2D(SFX.SuperActivateClip, 0.9f, 1.15f);
            var hud = FindFirstObjectByType<HUDController>();
            if (hud != null) hud.PulseVignette(ElementPalette.Base(Element.Solar), 0.45f);
        }

        protected override void Update()
        {
            base.Update();
            if (!active) return;
            if (Time.time >= endTime || !weapon.IsOverridden || weapon.OverrideRoundsLeft <= 0)
                EndGoldenGun();
        }

        /// <summary>Golden Gun kills detonate the victim; kills from the
        /// detonation detonate too, cascading outward. A started chain keeps
        /// running even if the super ends mid-cascade.</summary>
        private void OnGunKill(Health victim, Vector3 at)
            => StartCoroutine(ChainExplode(victim.transform.position + Vector3.up));

        private IEnumerator ChainExplode(Vector3 at)
        {
            FX.SpawnElementBurst(at, Element.Solar, 1.3f);
            SFX.Play3D(SFX.AbilityDetonateClip, at, 0.85f, 8f);

            var killed = new List<Health>(); // rare event; per-blast list is fine
            AoEDamage.Apply(at, explosionRadius, explosionDamage, gameObject, true, killed);

            foreach (var victim in killed)
            {
                if (victim == null) continue;
                yield return new WaitForSeconds(chainDelaySeconds);
                StartCoroutine(ChainExplode(victim.transform.position + Vector3.up));
            }
        }

        /// <summary>Golden shots get a traveling fire-bolt with trail and
        /// bursts layered over the hitscan tracer — purely cosmetic; the
        /// damage already landed the instant the trigger broke.</summary>
        private void OnGoldenShot(Vector3 muzzle, Vector3 end)
        {
            FX.SpawnElementBurst(muzzle, Element.Solar, 0.5f); // muzzle bloom

            var bolt = FX.SpawnBolt(muzzle, Element.Solar);
            bolt.name = "GoldenShot";
            bolt.transform.localScale = Vector3.one * 0.3f;
            FX.AddElementTrail(bolt, Element.Solar, 0.45f, shotTrailSeconds);

            Vector3 path = end - muzzle;
            const float speed = 90f;
            var proj = bolt.AddComponent<ThrownAbilityProjectile>();
            proj.gravity = 0f;
            proj.detonateOnImpact = true;
            proj.fuseSeconds = Mathf.Max(0.05f, path.magnitude / speed);
            proj.Detonated += at => FX.SpawnElementBurst(at, Element.Solar, 0.9f);
            proj.Launch(path.normalized * speed);
        }

        /// <summary>Kills while the super is in effect feed the next super.</summary>
        private void OnSuperKill() => AddEnergy(superKillRefund);

        private void EndGoldenGun()
        {
            active = false;
            weapon.TargetKilled -= OnGunKill;
            weapon.ShotFired -= OnGoldenShot;
            GameEvents.EnemyKilled -= OnSuperKill;
            if (weapon.IsOverridden) weapon.EndOverride(); // re-shows the frame model
            if (goldenViewmodel != null)
            {
                Destroy(goldenViewmodel.gameObject);
                goldenViewmodel = null;
            }
        }

        private static WeaponData BuildGoldenData()
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.displayName = "Golden Gun";
            d.frameName = "Solar";
            d.roundsPerMinute = 110f;
            d.bodyDamage = 300f;
            d.critMultiplier = 2f;
            d.falloffStart = 60f;
            d.falloffEnd = 120f;
            d.minDamageScale = 0.9f;
            d.magazineSize = 3;
            d.reloadSeconds = 999f; // never reloads; the override owns ammo
            d.adsZoomFov = 48f;
            d.adsSpeed = 8f;
            d.hipSpreadDegrees = 0.4f;
            d.adsSpreadDegrees = 0.05f;
            d.aimAssist = 90f;
            d.range = 90f;
            d.stability = 80f;
            d.recoilPitchKick = 5f;
            d.recoilYawKick = 1f;
            d.recoilRecoverySpeed = 10f;
            d.viewmodelKickback = 0.2f;
            d.tracerColor = new Color(1f, 0.6f, 0.15f, 0.95f); // solar
            return d;
        }
    }
}
