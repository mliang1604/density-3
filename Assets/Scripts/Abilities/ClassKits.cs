using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The canonical kit definitions — names and charge pacing for every
    /// class ability. Single source of truth shared by the editor bootstrap
    /// (which bakes them into ClassData assets) and ClassLoadout's runtime
    /// fallback (used before the first bake). Supers pace at a slow passive
    /// charge with meaningful kill bonuses and start empty.
    /// </summary>
    public static class ClassKits
    {
        public readonly struct AbilitySpec
        {
            public readonly string displayName;
            public readonly float cooldownSeconds;
            public readonly float energyOnKill;
            public readonly float startEnergy;

            public AbilitySpec(string name, float cooldown, float onKill, float start)
            {
                displayName = name;
                cooldownSeconds = cooldown;
                energyOnKill = onKill;
                startEnergy = start;
            }
        }

        private static readonly AbilitySpec[] warlock =
        {
            new AbilitySpec("Vortex Grenade", 60f, 0.05f, 1f),
            new AbilitySpec("Energy Drain", 75f, 0.10f, 1f),
            new AbilitySpec("Healing Rift", 70f, 0.05f, 1f),
            new AbilitySpec("Nova Bomb", 300f, 0.15f, 0f)
        };

        private static readonly AbilitySpec[] hunter =
        {
            new AbilitySpec("Tripmine Grenade", 60f, 0.05f, 1f),
            new AbilitySpec("Throwing Knife", 75f, 0.10f, 1f),
            new AbilitySpec("Marksman's Dodge", 30f, 0.03f, 1f),
            new AbilitySpec("Golden Gun", 300f, 0.15f, 0f)
        };

        private static readonly AbilitySpec[] titan =
        {
            new AbilitySpec("Pulse Grenade", 60f, 0.05f, 1f),
            new AbilitySpec("Seismic Strike", 75f, 0.10f, 1f),
            new AbilitySpec("Towering Barricade", 45f, 0.04f, 1f),
            new AbilitySpec("Fists of Havoc", 300f, 0.15f, 0f)
        };

        /// <summary>Specs in slot order: grenade, melee, class ability, super.</summary>
        public static AbilitySpec[] KitFor(GuardianClass g) =>
            g == GuardianClass.Hunter ? hunter :
            g == GuardianClass.Titan ? titan :
            warlock;

        /// <summary>Fills a ClassData (and four fresh AbilityData instances)
        /// with the class's canonical kit.</summary>
        public static void Configure(ClassData c, GuardianClass g)
        {
            c.guardianClass = g;
            c.className = g.ToString();
            c.element = g.ElementOf();

            var kit = KitFor(g);
            c.grenade = Make(kit[0], c.element);
            c.melee = Make(kit[1], c.element);
            c.classAbility = Make(kit[2], c.element);
            c.super = Make(kit[3], c.element);
        }

        private static AbilityData Make(AbilitySpec s, Element e)
        {
            var a = ScriptableObject.CreateInstance<AbilityData>();
            a.name = s.displayName;
            a.displayName = s.displayName;
            a.element = e;
            a.cooldownSeconds = s.cooldownSeconds;
            a.energyOnKill = s.energyOnKill;
            a.startEnergy = s.startEnergy;
            return a;
        }
    }
}
