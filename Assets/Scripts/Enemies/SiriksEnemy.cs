using UnityEngine;

namespace Density3.Enemies
{
    /// <summary>
    /// Siriks, Light Turned — the Zero Hour boss. Built on the Captain brain
    /// (volleys + claw swipes); the phase state machine lands with #39 and
    /// the immunity gates with #40. The boss pool never regens, and the
    /// EncounterDirector's no-respawn spawn path means its death is final —
    /// which the mission flow treats as the win.
    /// </summary>
    public class SiriksEnemy : CaptainEnemy
    {
        /// <summary>Canonical Siriks tuning — shared by the bootstrap bake and
        /// the runtime fallback (ClassKits.Configure pattern).</summary>
        public static new void Configure(EnemyData d)
        {
            d.displayName = "Siriks";
            d.maxHealth = 2000f; // boss pool, no regen
            d.moveSpeed = 4.6f;
            d.strafeSpeed = 2.2f;
            d.aggroRange = 70f;  // the whole vault room
            d.preferredRange = 7f;
            d.fireRange = 45f;
            d.fireInterval = 4f;
            d.projectileDamage = 11f; // per bolt; volleys are the threat
            d.projectileSpeed = 22f;
        }

        protected override EnemyData DefaultData()
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            Configure(d);
            return d;
        }
    }
}
