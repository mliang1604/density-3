using System;
using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// Base of all class abilities: owns the 0..1 energy bar (passive regen
    /// paced by AbilityData.cooldownSeconds, plus kill bonuses via
    /// GameEvents.EnemyKilled), gates activation on a full bar, and raises
    /// the events the HUD and audio layers listen to. Subclasses implement
    /// OnActivate with the ability's actual effect; refund mechanics
    /// (energy-drain melee, Knife Juggler) call AddEnergy.
    /// </summary>
    public abstract class AbilityBase : MonoBehaviour
    {
        public AbilityData data;

        /// <summary>Charge from 0 (empty) to 1 (ready to cast).</summary>
        public float Energy { get; private set; }

        public bool IsReady => Energy >= 1f;

        /// <summary>Raised after the ability fires (energy already spent).</summary>
        public event Action Activated;

        /// <summary>Raised when the bar crosses full, in either direction.</summary>
        public event Action<bool> ReadyChanged;

        private bool wasReady;

        protected virtual void Awake()
        {
            if (data != null) Energy = data.startEnergy;
            wasReady = IsReady;
        }

        protected virtual void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;
        protected virtual void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        protected virtual void Update()
        {
            if (data == null || data.cooldownSeconds <= 0f || IsReady) return;
            SetEnergy(Energy + Time.deltaTime / data.cooldownSeconds);
        }

        /// <summary>Spends the full bar and fires the ability when charged.</summary>
        public bool TryActivate()
        {
            if (data == null || !IsReady || !CanActivate()) return false;
            SetEnergy(0f);
            OnActivate();
            Activated?.Invoke();
            return true;
        }

        /// <summary>Subclass gate for situational requirements (grounded casts
        /// and the like). Checked before the bar is spent, so a refused
        /// activation costs nothing.</summary>
        protected virtual bool CanActivate() => true;

        /// <summary>Grants bonus energy (refund mechanics, pickups, tuning hooks).</summary>
        public void AddEnergy(float amount) => SetEnergy(Energy + amount);

        /// <summary>Assigns data and resets the charge to its starting value —
        /// for abilities attached at runtime (ClassLoadout), where Awake has
        /// already run with no data.</summary>
        public void Bind(AbilityData abilityData)
        {
            data = abilityData;
            Energy = data != null ? data.startEnergy : 0f;
            wasReady = IsReady;
        }

        /// <summary>The ability's effect. Energy is already spent when this runs.</summary>
        protected abstract void OnActivate();

        private void OnEnemyKilled()
        {
            if (data != null) SetEnergy(Energy + data.energyOnKill);
        }

        private void SetEnergy(float value)
        {
            Energy = Mathf.Clamp01(value);
            if (IsReady != wasReady)
            {
                wasReady = IsReady;
                ReadyChanged?.Invoke(wasReady);
            }
        }
    }
}
