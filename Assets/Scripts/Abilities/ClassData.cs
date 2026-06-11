using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// One playable class: identity plus its four ability definitions.
    /// Baked into Resources/Classes by the editor bootstrap so ClassLoadout
    /// can resolve the selected class at runtime with no scene or prefab
    /// references.
    /// </summary>
    [CreateAssetMenu(menuName = "Density3/Class Data", fileName = "NewClassData")]
    public class ClassData : ScriptableObject
    {
        public GuardianClass guardianClass = GuardianClass.Warlock;
        public string className = "Warlock";
        public Element element = Element.Void;

        [Header("Kit")]
        public AbilityData grenade;
        public AbilityData melee;
        public AbilityData classAbility;
        public AbilityData super;
    }
}
