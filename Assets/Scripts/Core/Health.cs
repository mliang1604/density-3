using System;
using UnityEngine;

namespace FableFPS.Core
{
    /// <summary>
    /// Generic health pool. Set regenRate > 0 for Destiny-style recharging
    /// shields (used by the player; enemies leave it at 0).
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;

        [Header("Regen (player-style shields)")]
        public float regenDelay = 0f;
        public float regenRate = 0f;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }

        /// <summary>The most recent damage applied — lets death handlers know
        /// whether the killing blow was a precision (crit) hit, and from whom.</summary>
        public DamageInfo LastDamage { get; private set; }

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        private float lastDamageTime = -999f;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void Update()
        {
            if (IsDead || regenRate <= 0f || Current >= maxHealth) return;
            if (Time.time - lastDamageTime < regenDelay) return;
            Current = Mathf.Min(maxHealth, Current + regenRate * Time.deltaTime);
        }

        public void SetMaxHealth(float value, bool refill = true)
        {
            maxHealth = value;
            if (refill) Current = value;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (IsDead) return;
            LastDamage = info;
            lastDamageTime = Time.time;
            Current = Mathf.Max(0f, Current - info.amount);
            Damaged?.Invoke(info);
            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
            }
        }

        public void Revive()
        {
            IsDead = false;
            Current = maxHealth;
        }
    }

    public struct DamageInfo
    {
        public float amount;
        public bool isCrit;
        public Vector3 hitPoint;
        public GameObject source;
    }
}
