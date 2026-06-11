using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Weapons
{
    /// <summary>
    /// Semi-automatic hitscan hand cannon: RPM-gated trigger, ADS zoom,
    /// recoil, damage falloff, crits, reload, and frame swapping (1/2/3).
    /// </summary>
    public class HandCannon : MonoBehaviour
    {
        public WeaponData[] loadout;

        [Header("Wiring (prefab references)")]
        public PlayerController player;
        public Camera cam;
        public Transform viewmodel;          // sway/kick root, parent of all frame models
        public WeaponViewmodel[] viewmodels; // one per loadout entry
        public BulletMagnetism magnetism;

        private bool magnetismAds;

        private readonly Vector3 hipPos = new Vector3(0.26f, -0.26f, 0.5f);
        private readonly Vector3 adsPos = new Vector3(0f, -0.175f, 0.42f);

        private int currentIndex;
        private int[] magState;
        private float nextFireTime;
        private bool reloading;
        private float reloadEndTime;
        private float adsBlend;
        private float reloadDip;
        private float kick;
        private float baseFov;
        private float muzzleLightOff;

        public WeaponData Current =>
            (loadout != null && loadout.Length > 0)
                ? loadout[Mathf.Clamp(currentIndex, 0, loadout.Length - 1)]
                : null;

        public int RoundsInMag => magState != null ? magState[currentIndex] : 0;
        public bool IsReloading => reloading;

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerController>();
            if (magnetism == null) magnetism = GetComponent<BulletMagnetism>();
            baseFov = cam != null ? cam.fieldOfView : 75f;

            if (loadout == null || loadout.Length == 0)
                loadout = new[] { DefaultData() };

            magState = new int[loadout.Length];
            for (int i = 0; i < loadout.Length; i++)
                magState[i] = loadout[i].magazineSize;

            SetViewmodelIndex(currentIndex);
            PushMagnetismStats();
        }

        private WeaponViewmodel ActiveViewmodel =>
            (viewmodels != null && viewmodels.Length > 0)
                ? viewmodels[Mathf.Clamp(currentIndex, 0, viewmodels.Length - 1)]
                : null;

        /// <summary>Shows only the model matching the equipped frame.</summary>
        private void SetViewmodelIndex(int index)
        {
            if (viewmodels == null) return;
            for (int i = 0; i < viewmodels.Length; i++)
            {
                if (viewmodels[i] == null) continue;
                viewmodels[i].gameObject.SetActive(i == index);
                if (viewmodels[i].muzzleLight != null) viewmodels[i].muzzleLight.enabled = false;
            }
        }

        private void PushMagnetismStats()
        {
            if (magnetism != null && Current != null)
                magnetism.SetStats(Current.aimAssist, Current.range, Current.stability);
        }

        private static WeaponData DefaultData()
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.displayName = "Prototype 140";
            return d;
        }

        private void Update()
        {
            if (player == null || Current == null) return;

            if (player.MovementLocked)
            {
                adsBlend = Mathf.MoveTowards(adsBlend, 0f, 10f * Time.deltaTime);
                ApplyAdsEffects();
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;

            HandleSwitching();
            var data = Current;

            bool wantsAds = Input.GetMouseButton(1) && !reloading;
            adsBlend = Mathf.MoveTowards(adsBlend, wantsAds ? 1f : 0f, data.adsSpeed * Time.deltaTime);
            ApplyAdsEffects();

            // Magnetism tracks ADS state changes (SetADS also resets bloom, per D2).
            bool adsNow = adsBlend > 0.5f;
            if (magnetism != null && adsNow != magnetismAds)
            {
                magnetismAds = adsNow;
                magnetism.SetADS(adsNow);
            }

            if (reloading && Time.time >= reloadEndTime)
            {
                reloading = false;
                magState[currentIndex] = data.magazineSize;
                SFX.Play2D(SFX.ReloadEndClip, 0.6f);
            }

            if (!reloading && Input.GetKeyDown(KeyCode.R) && magState[currentIndex] < data.magazineSize)
                StartReload(data);

            // Semi-auto: one shot per click, gated by frame RPM.
            if (!reloading && Input.GetMouseButtonDown(0) && Time.time - player.CursorLockedAt > 0.15f)
            {
                if (magState[currentIndex] <= 0)
                {
                    SFX.Play2D(SFX.DryFireClip, 0.5f);
                    StartReload(data);
                }
                else if (Time.time >= nextFireTime) Fire(data);
            }

            var vmActive = ActiveViewmodel;
            if (vmActive != null && vmActive.muzzleLight != null && vmActive.muzzleLight.enabled
                && Time.time >= muzzleLightOff)
                vmActive.muzzleLight.enabled = false;
        }

        private void ApplyAdsEffects()
        {
            var data = Current;
            float t = Mathf.SmoothStep(0f, 1f, adsBlend);
            cam.fieldOfView = Mathf.Lerp(baseFov, data.adsZoomFov, t);
            player.SensitivityScale = Mathf.Lerp(1f, data.adsZoomFov / baseFov, t);
            player.SpeedScale = Mathf.Lerp(1f, player.adsSpeedScale, t);
        }

        private void HandleSwitching()
        {
            for (int i = 0; i < loadout.Length && i < 3; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && i != currentIndex)
                {
                    currentIndex = i;
                    reloading = false;
                    kick += 0.15f;
                    nextFireTime = Time.time + 0.35f;
                    SFX.Play2D(SFX.ReloadStartClip, 0.4f, 1.25f);
                    SetViewmodelIndex(i);
                    PushMagnetismStats();
                }
            }
        }

        private void StartReload(WeaponData data)
        {
            if (reloading) return;
            reloading = true;
            reloadEndTime = Time.time + data.reloadSeconds;
            SFX.Play2D(SFX.ReloadStartClip, 0.55f);
        }

        private void Fire(WeaponData data)
        {
            magState[currentIndex]--;
            nextFireTime = Time.time + data.SecondsBetweenShots;
            SFX.PlayGunshot(data.roundsPerMinute, 0.85f);

            float spread = Mathf.Lerp(data.hipSpreadDegrees, data.adsSpreadDegrees, adsBlend);
            Vector2 c = Random.insideUnitCircle * Mathf.Tan(spread * Mathf.Deg2Rad);
            Vector3 dir = cam.transform.TransformDirection(new Vector3(c.x, c.y, 1f).normalized);

            // Bullet magnetism bends the shot toward a nearby enemy, then blooms the cone.
            if (magnetism != null)
            {
                dir = magnetism.CalculateMagnetizedDirection(cam.transform.position, dir);
                magnetism.ApplyShotBloom();
            }

            Vector3 end = cam.transform.position + dir * data.maxHitDistance;
            if (Physics.Raycast(cam.transform.position, dir, out RaycastHit hit, data.maxHitDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var hb = hit.collider.GetComponent<Hitbox>();
                if (hb != null)
                {
                    float dmg = data.bodyDamage * FalloffScale(data, hit.distance);
                    float applied = hb.Hit(dmg, data.critMultiplier, hit.point, gameObject);
                    if (applied > 0f)
                        DamageNumbers.Spawn(hit.point, applied, hb.isCritZone);
                }
                else
                {
                    FX.SpawnImpact(hit.point, hit.normal);
                }
            }

            var vm = ActiveViewmodel;
            Vector3 muzzlePos = (vm != null && vm.muzzlePoint != null)
                ? vm.muzzlePoint.position
                : cam.transform.position + dir * 0.4f;
            FX.SpawnTracer(muzzlePos, end, new Color(1f, 0.9f, 0.6f, 0.9f));
            if (vm != null && vm.muzzleLight != null)
            {
                vm.muzzleLight.enabled = true;
                muzzleLightOff = Time.time + 0.05f;
            }

            player.AddRecoil(data.recoilPitchKick * Mathf.Lerp(1f, 0.7f, adsBlend), data.recoilYawKick);
            player.SetRecoilRecovery(data.recoilRecoverySpeed);
            kick += data.viewmodelKickback;
        }

        private static float FalloffScale(WeaponData d, float dist)
        {
            if (dist <= d.falloffStart) return 1f;
            if (dist >= d.falloffEnd) return d.minDamageScale;
            return Mathf.Lerp(1f, d.minDamageScale, (dist - d.falloffStart) / (d.falloffEnd - d.falloffStart));
        }

        public void RefillMag()
        {
            if (magState != null && Current != null)
                magState[currentIndex] = Current.magazineSize;
            reloading = false;
        }

        private void LateUpdate()
        {
            if (viewmodel == null || Current == null) return;

            kick = Mathf.Lerp(kick, 0f, 12f * Time.deltaTime);
            reloadDip = Mathf.MoveTowards(reloadDip, reloading ? 1f : 0f, 6f * Time.deltaTime);

            float t = Mathf.SmoothStep(0f, 1f, adsBlend);
            Vector3 pos = Vector3.Lerp(hipPos, adsPos, t);
            pos += new Vector3(0f, kick * 0.25f, -kick);
            viewmodel.localPosition = pos;
            viewmodel.localRotation = Quaternion.Euler(-kick * 70f + reloadDip * 40f, 0f, 0f);
        }
    }
}
