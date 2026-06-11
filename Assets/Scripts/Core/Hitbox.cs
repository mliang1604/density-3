using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Attach next to any collider to route weapon hits to the owning Health.
    /// Crit zones (heads) apply the weapon's precision multiplier.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        public Health owner;
        public bool isCritZone;

        /// <summary>Returns the damage actually applied (0 when the owner is missing or already dead).</summary>
        public float Hit(float baseDamage, float critMultiplier, Vector3 point, GameObject source)
        {
            if (owner == null || owner.IsDead) return 0f;
            float amount = isCritZone ? baseDamage * critMultiplier : baseDamage;
            owner.ApplyDamage(new DamageInfo
            {
                amount = amount,
                isCrit = isCritZone,
                hitPoint = point,
                source = source
            });
            return amount;
        }
    }
}
