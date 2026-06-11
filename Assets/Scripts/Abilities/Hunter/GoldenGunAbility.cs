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
    /// riding the existing HandCannon machinery via its weapon-override API.
    /// The active viewmodel is tinted gold for the duration (shared
    /// materials saved and restored — never mutated). Ends after three
    /// shots or the timer; the previous weapon returns with its exact mag.
    /// </summary>
    public class GoldenGunAbility : AbilityBase
    {
        public float durationSeconds = 12f;
        public int rounds = 3;
        public float explosionDamage = 160f;
        public float explosionRadius = 4f;
        public float chainDelaySeconds = 0.15f; // the chain reads as a cascade, not one blast

        private static Material goldMaterial;

        private HandCannon weapon;
        private PlayerController player;
        private WeaponData goldenData;
        private float endTime;
        private bool active;
        private readonly List<Renderer> tintedRenderers = new List<Renderer>();
        private readonly List<Material[]> savedMaterials = new List<Material[]>();

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

            weapon.BeginOverride(goldenData, rounds);
            weapon.TargetKilled += OnGunKill;
            TintActiveViewmodel();
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

        private void EndGoldenGun()
        {
            active = false;
            weapon.TargetKilled -= OnGunKill;
            if (weapon.IsOverridden) weapon.EndOverride();
            for (int i = 0; i < tintedRenderers.Count; i++)
                if (tintedRenderers[i] != null)
                    tintedRenderers[i].sharedMaterials = savedMaterials[i];
            tintedRenderers.Clear();
            savedMaterials.Clear();
        }

        private void TintActiveViewmodel()
        {
            if (goldMaterial == null)
            {
                goldMaterial = new Material(Shader.Find("Standard"))
                    { color = new Color(1f, 0.78f, 0.25f) };
                goldMaterial.SetFloat("_Metallic", 0.85f);
                goldMaterial.SetFloat("_Glossiness", 0.75f);
                goldMaterial.EnableKeyword("_EMISSION");
                goldMaterial.SetColor("_EmissionColor", ElementPalette.Emission(Element.Solar, 0.6f));
            }

            if (weapon.viewmodels == null) return;
            foreach (var vm in weapon.viewmodels)
            {
                if (vm == null || !vm.gameObject.activeSelf) continue;
                foreach (var r in vm.GetComponentsInChildren<Renderer>())
                {
                    tintedRenderers.Add(r);
                    savedMaterials.Add(r.sharedMaterials);
                    var golden = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < golden.Length; i++) golden[i] = goldMaterial;
                    r.sharedMaterials = golden;
                }
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
