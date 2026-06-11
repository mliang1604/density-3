using UnityEngine;

namespace Density3.Weapons
{
    /// <summary>
    /// Hand cannon archetype definition (Destiny-style frame).
    /// Defaults model a 140 RPM Adaptive Frame.
    /// </summary>
    [CreateAssetMenu(menuName = "Density3/Weapon Data", fileName = "NewWeaponData")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Hand Cannon";
        public string frameName = "Adaptive Frame";

        [Header("Firing")]
        public float roundsPerMinute = 140f;
        public float bodyDamage = 45f;
        public float critMultiplier = 1.6f;

        [Header("Range / Falloff")]
        public float falloffStart = 22f;
        public float falloffEnd = 40f;
        [Range(0.1f, 1f)] public float minDamageScale = 0.55f;
        public float maxHitDistance = 300f;

        [Header("Magazine")]
        public int magazineSize = 12;
        public float reloadSeconds = 1.9f;

        [Header("Handling")]
        public float adsZoomFov = 48f;
        public float adsSpeed = 6f;
        public float hipSpreadDegrees = 1.6f;
        public float adsSpreadDegrees = 0.15f;

        [Header("Bullet Magnetism (0-100, Destiny-style stats)")]
        [Range(0f, 100f)] public float aimAssist = 75f;
        [Range(0f, 100f)] public float range = 60f;
        [Range(0f, 100f)] public float stability = 55f;

        [Header("Recoil")]
        public float recoilPitchKick = 2.6f;
        public float recoilYawKick = 0.5f;
        public float recoilRecoverySpeed = 8f;
        public float viewmodelKickback = 0.12f;

        public float SecondsBetweenShots => 60f / Mathf.Max(1f, roundsPerMinute);
    }
}
