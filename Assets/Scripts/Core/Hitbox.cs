using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Attach next to any collider to route weapon hits to the owning Health.
    /// Crit zones (heads) apply the weapon's precision multiplier — unless an
    /// EnergyShield is up, which gates precision until it breaks.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Health owner;
        public bool isCritZone;

        /// <summary>True when the most recent Hit counted as a precision hit
        /// (crit zones stop counting while an energy shield is up).</summary>
        public bool LastHitWasCrit { get; private set; }

        /// <summary>True when the most recent Hit landed on an active energy
        /// shield — damage numbers tint to the shield's element.</summary>
        public bool LastHitShielded { get; private set; }

        /// <summary>The owner's shield, when it has one.</summary>
        public EnergyShield Shield
        {
            get
            {
                if (!shieldChecked)
                {
                    shield = owner != null ? owner.GetComponent<EnergyShield>() : null;
                    shieldChecked = true;
                }
                return shield;
            }
        }

        private EnergyShield shield;
        private bool shieldChecked;

        /// <summary>Returns the damage actually applied (0 when the owner is missing or already dead).</summary>
        public float Hit(float baseDamage, float critMultiplier, Vector3 point, GameObject source)
        {
            if (owner == null || owner.IsDead) return 0f;

            bool shielded = Shield != null && Shield.IsUp;
            bool crit = isCritZone && !shielded; // precision counts on bare health only
            LastHitWasCrit = crit;
            LastHitShielded = shielded;

            float amount = crit ? baseDamage * critMultiplier : baseDamage;
            owner.ApplyDamage(new DamageInfo
            {
                amount = amount,
                isCrit = crit,
                hitPoint = point,
                source = source
            });
            return amount;
        }
    }
}
