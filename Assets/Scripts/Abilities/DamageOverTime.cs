using UnityEngine;
using Density3.Core;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Ticking damage on one Health (solar burns, void siphons). Applied via
    /// the static helper, which stacks by refreshing: duration extends to the
    /// longest remaining and dps takes the strongest source. Removes itself
    /// when the timer runs out or the target dies.
    /// </summary>
    public class DamageOverTime : MonoBehaviour
    {
        private const float TickInterval = 0.25f;

        private Health health;
        private float dps;
        private float remaining;
        private float tickTimer;
        private GameObject source;

        public static void Apply(Health target, float damagePerSecond, float seconds, GameObject damageSource)
        {
            if (target == null || target.IsDead) return;
            var dot = target.GetComponent<DamageOverTime>();
            if (dot == null) dot = target.gameObject.AddComponent<DamageOverTime>();
            dot.health = target;
            dot.dps = Mathf.Max(dot.remaining > 0f ? dot.dps : 0f, damagePerSecond);
            dot.remaining = Mathf.Max(dot.remaining, seconds);
            dot.source = damageSource;
        }

        private void Update()
        {
            if (health == null || health.IsDead)
            {
                Destroy(this);
                return;
            }

            remaining -= Time.deltaTime;
            tickTimer += Time.deltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                float amount = dps * TickInterval;
                Vector3 at = transform.position + Vector3.up;
                health.ApplyDamage(new DamageInfo
                {
                    amount = amount,
                    isCrit = false,
                    hitPoint = at,
                    source = source
                });
                DamageNumbers.Spawn(at, amount, false);
            }

            if (remaining <= 0f) Destroy(this);
        }
    }
}
