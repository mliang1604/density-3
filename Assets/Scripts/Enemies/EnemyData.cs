using UnityEngine;

namespace Density3.Enemies
{
    /// <summary>
    /// Fallen combatant archetype definition (WeaponData pattern). One asset
    /// per enemy type holds the whole balance surface: vitality, movement,
    /// engagement ranges, and ranged-attack stats. Class defaults model the
    /// classic Dreg, so a missing asset degrades to Dreg behavior.
    /// </summary>
    [CreateAssetMenu(menuName = "Density3/Enemy Data", fileName = "NewEnemyData")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Dreg";

        [Header("Vitality")]
        public float maxHealth = 150f;

        [Header("Movement")]
        public float moveSpeed = 4.5f;
        public float strafeSpeed = 3f;

        [Header("Engagement Ranges")]
        public float aggroRange = 45f;
        public float preferredRange = 14f;
        public float fireRange = 30f;

        [Header("Ranged Attack")]
        public float fireInterval = 2.2f;
        public float projectileDamage = 14f;
        public float projectileSpeed = 17f;
    }
}
