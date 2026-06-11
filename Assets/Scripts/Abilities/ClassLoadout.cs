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
            slots.grenade = Attach(Active.grenade);
            slots.melee = Attach(Active.melee);
            slots.classAbility = Attach(Active.classAbility);
            slots.super = Attach(Active.super);
        }

        // Placeholder component until the class kits land (M2-M4 swap in
        // concrete AbilityBase subclasses per slot).
        private AbilityBase Attach(AbilityData data)
        {
            if (data == null) return null;
            var a = gameObject.AddComponent<DebugAbility>();
            a.autoActivate = false;
            a.Bind(data);
            return a;
        }

        private static ClassData RuntimeDefault(GuardianClass g)
        {
            var c = ScriptableObject.CreateInstance<ClassData>();
            ClassKits.Configure(c, g);
            return c;
        }
    }
}
