using UnityEngine;
using Density3.Weapons;

namespace Density3.Abilities
{
    /// <summary>
    /// Builds the Golden Gun's dedicated viewmodel at runtime from primitives
    /// — the runtime cousin of the bootstrap's baked frame viewmodels. A long
    /// ornate gold revolver with flame accents, a constant warm glow, and its
    /// own muzzle point + light wrapped in a WeaponViewmodel so HandCannon
    /// fires from it directly.
    /// </summary>
    public static class GoldenGunViewmodel
    {
        private static Material gold;
        private static Material dark;
        private static Material flame;

        public static WeaponViewmodel Build(Transform parent)
        {
            EnsureMaterials();
            var root = new GameObject("VM_GoldenGun") { layer = 2 };
            root.transform.SetParent(parent, false);
            var t = root.transform;

            Part(t, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.045f, 0.085f, 0.26f), gold);
            Part(t, PrimitiveType.Cube, "TopRib", new Vector3(0f, 0.075f, 0.12f), new Vector3(0.024f, 0.014f, 0.34f), dark);
            Part(t, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.046f, 0.26f), new Vector3(0.03f, 0.11f, 0.03f), gold, Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, "MuzzleCrown", new Vector3(0f, 0.046f, 0.365f), new Vector3(0.04f, 0.012f, 0.04f), flame, Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, "Drum", new Vector3(0f, 0.004f, 0f), new Vector3(0.075f, 0.028f, 0.075f), gold, Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, "DrumBand", new Vector3(0f, 0.004f, 0.026f), new Vector3(0.077f, 0.005f, 0.077f), flame, Quaternion.Euler(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "Hammer", new Vector3(0f, 0.064f, -0.105f), new Vector3(0.014f, 0.042f, 0.018f), dark, Quaternion.Euler(-20f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "HammerSpur", new Vector3(0f, 0.09f, -0.118f), new Vector3(0.019f, 0.01f, 0.026f), dark, Quaternion.Euler(-50f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "GuardBottom", new Vector3(0f, -0.064f, 0f), new Vector3(0.013f, 0.008f, 0.058f), dark);
            Part(t, PrimitiveType.Cube, "GuardFront", new Vector3(0f, -0.047f, 0.028f), new Vector3(0.013f, 0.03f, 0.008f), dark);
            Part(t, PrimitiveType.Cube, "Trigger", new Vector3(0f, -0.041f, 0.002f), new Vector3(0.007f, 0.023f, 0.007f), dark, Quaternion.Euler(12f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.108f, -0.072f), new Vector3(0.038f, 0.13f, 0.05f), dark, Quaternion.Euler(18f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "GripCap", new Vector3(0f, -0.172f, -0.09f), new Vector3(0.042f, 0.013f, 0.054f), gold, Quaternion.Euler(18f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "FrontSight", new Vector3(0f, 0.098f, 0.345f), new Vector3(0.007f, 0.024f, 0.016f), dark);
            Part(t, PrimitiveType.Cube, "FiberOptic", new Vector3(0f, 0.108f, 0.345f), new Vector3(0.007f, 0.007f, 0.01f), flame);

            // Flame vents marching up the barrel — the gun reads as burning.
            Part(t, PrimitiveType.Cube, "FlameVent", new Vector3(0f, 0.068f, 0.16f), new Vector3(0.018f, 0.006f, 0.03f), flame);
            Part(t, PrimitiveType.Cube, "FlameVent", new Vector3(0f, 0.068f, 0.22f), new Vector3(0.018f, 0.006f, 0.03f), flame);
            Part(t, PrimitiveType.Cube, "FlameVent", new Vector3(0f, 0.068f, 0.28f), new Vector3(0.018f, 0.006f, 0.03f), flame);

            var muzzleGO = new GameObject("MuzzlePoint") { layer = 2 };
            muzzleGO.transform.SetParent(root.transform, false);
            muzzleGO.transform.localPosition = new Vector3(0f, 0.046f, 0.39f);

            var lightGO = new GameObject("MuzzleLight") { layer = 2 };
            lightGO.transform.SetParent(muzzleGO.transform, false);
            var muzzleLight = lightGO.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1f, 0.6f, 0.2f);
            muzzleLight.intensity = 4.5f;
            muzzleLight.range = 7f;
            muzzleLight.enabled = false;

            // Constant warm glow in the hands for the super's duration.
            var glowGO = new GameObject("Glow") { layer = 2 };
            glowGO.transform.SetParent(root.transform, false);
            glowGO.transform.localPosition = new Vector3(0f, 0.02f, 0.08f);
            var glow = glowGO.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.65f, 0.25f);
            glow.intensity = 1.6f;
            glow.range = 1.1f;

            var vm = root.AddComponent<WeaponViewmodel>();
            vm.muzzlePoint = muzzleGO.transform;
            vm.muzzleLight = muzzleLight;
            return vm;
        }

        private static void EnsureMaterials()
        {
            if (gold != null) return;
            gold = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.78f, 0.25f) };
            gold.SetFloat("_Metallic", 0.85f);
            gold.SetFloat("_Glossiness", 0.75f);
            gold.EnableKeyword("_EMISSION");
            gold.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.15f) * 0.35f);
            dark = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.12f, 0.06f) };
            dark.SetFloat("_Metallic", 0.6f);
            dark.SetFloat("_Glossiness", 0.55f);
            flame = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.5f, 0.1f) };
            flame.EnableKeyword("_EMISSION");
            flame.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2.2f);
        }

        private static void Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 scale, Material mat)
            => Part(parent, type, name, localPos, scale, mat, Quaternion.identity);

        private static void Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
