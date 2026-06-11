using UnityEditor;
using UnityEngine;
using Density3.Abilities;
using Density3.Core;

namespace Density3.EditorTools
{
    public static partial class ProjectBootstrap
    {
        // ----- Class & ability data --------------------------------------------

        /// <summary>
        /// Bakes one ClassData asset per class into Resources/Classes — loaded
        /// by ClassLoadout at runtime, so no scene or prefab references are
        /// needed — with the four AbilityData entries as sub-assets so each
        /// class is a single editable file. Existing assets are preserved;
        /// delete one to regenerate it from the canonical ClassKits.
        /// </summary>
        private static void BuildClasses()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Classes");

            foreach (GuardianClass g in System.Enum.GetValues(typeof(GuardianClass)))
            {
                string path = "Assets/Resources/Classes/" + g + ".asset";
                if (AssetDatabase.LoadAssetAtPath<ClassData>(path) != null) continue;

                var c = ScriptableObject.CreateInstance<ClassData>();
                ClassKits.Configure(c, g);
                AssetDatabase.CreateAsset(c, path);
                AddAbilitySubAsset(c.grenade, c);
                AddAbilitySubAsset(c.melee, c);
                AddAbilitySubAsset(c.classAbility, c);
                AddAbilitySubAsset(c.super, c);
                AssetDatabase.ImportAsset(path);
            }
        }

        private static void AddAbilitySubAsset(AbilityData ability, ClassData owner)
        {
            if (ability == null) return;
            AssetDatabase.AddObjectToAsset(ability, owner);
        }
    }
}
