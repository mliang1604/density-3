using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// Applies the selected class to the player at spawn: resolves the
    /// ClassData baked in Resources/Classes (falling back to the canonical
    /// ClassKits defaults before the first bake), then fills the
    /// PlayerAbilities slots. Until the concrete class kits land, every
    /// slot is a DebugAbility placeholder — input-fired with a log and a
    /// chime — so binds, meters, and events are exercised end to end.
    /// </summary>
    public class ClassLoadout : MonoBehaviour
    {
        public ClassData Active { get; private set; }

        private void Awake()
        {
            var want = GameManager.SelectedClass;
            foreach (var c in Resources.LoadAll<ClassData>("Classes"))
                if (c.guardianClass == want) { Active = c; break; }
            if (Active == null) Active = RuntimeDefault(want);

            var slots = GetComponent<PlayerAbilities>();
            if (slots == null) slots = gameObject.AddComponent<PlayerAbilities>();
            slots.grenade = Attach(Active.grenade, AbilitySlot.Grenade);
            slots.melee = Attach(Active.melee, AbilitySlot.Melee);
            slots.classAbility = Attach(Active.classAbility, AbilitySlot.ClassAbility);
            slots.super = Attach(Active.super, AbilitySlot.Super);
        }

        private AbilityBase Attach(AbilityData data, AbilitySlot slot)
        {
            if (data == null) return null;
            var ability = CreateAbility(slot);
            ability.Bind(data);
            return ability;
        }

        /// <summary>Concrete kit components per class. Classes whose kits
        /// haven't landed yet get the DebugAbility placeholder.</summary>
        private AbilityBase CreateAbility(AbilitySlot slot)
        {
            if (Active.guardianClass == GuardianClass.Warlock)
            {
                switch (slot)
                {
                    case AbilitySlot.Grenade: return gameObject.AddComponent<VortexGrenadeAbility>();
                    case AbilitySlot.Melee: return gameObject.AddComponent<EnergyDrainMelee>();
                    case AbilitySlot.ClassAbility: return gameObject.AddComponent<HealingRiftAbility>();
                    case AbilitySlot.Super: return gameObject.AddComponent<NovaBombAbility>();
                }
            }

            var placeholder = gameObject.AddComponent<DebugAbility>();
            placeholder.autoActivate = false;
            return placeholder;
        }

        private static ClassData RuntimeDefault(GuardianClass g)
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            ClassKits.Configure(c, g);
            return c;
        }
    }
}
