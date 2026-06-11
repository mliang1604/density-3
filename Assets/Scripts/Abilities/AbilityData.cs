using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// Ability archetype definition (Destiny-style): identity plus charge
    /// pacing. Behavior lives in AbilityBase subclasses; one asset per
    /// ability, baked by the editor bootstrap. Defaults model a
    /// mid-cooldown grenade.
    /// </summary>
    [CreateAssetMenu(menuName = "Density3/Ability Data", fileName = "NewAbilityData")]
    public class AbilityData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Ability";
        public Element element = Element.Void;

        [Header("Charge")]
        [Tooltip("Seconds from empty to full via passive regen. 0 disables passive charge (super-style: kills only).")]
        public float cooldownSeconds = 60f;
        [Range(0f, 1f)]
        [Tooltip("Bonus energy per enemy kill (1 = a full bar).")]
        public float energyOnKill = 0.05f;
        [Range(0f, 1f)]
        [Tooltip("Charge at spawn. Grenade/melee/class abilities start ready; supers start empty.")]
        public float startEnergy = 1f;
    }
}
