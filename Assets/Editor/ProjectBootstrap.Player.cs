using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Density3.Abilities;
using Density3.Core;
using Density3.Enemies;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.EditorTools
{
    public static partial class ProjectBootstrap
    {
        // ----- Weapons --------------------------------------------------------

        private static WeaponData[] BuildWeapons()
        {
            var adaptive = CreateWeapon("HC_Adaptive_140", w =>
            {
                w.displayName = "Last Riposte";
                w.frameName = "Adaptive Frame";
                w.aimAssist = 75f;
                w.range = 60f;
                w.stability = 55f;
            });

            var aggressive = CreateWeapon("HC_Aggressive_120", w =>
            {
                w.displayName = "Iron Remit";
                w.frameName = "Aggressive Frame";
                w.roundsPerMinute = 120f;
                w.bodyDamage = 55f;
                w.critMultiplier = 1.62f;
                w.falloffStart = 26f;
                w.falloffEnd = 46f;
                w.magazineSize = 10;
                w.reloadSeconds = 2.2f;
                w.adsZoomFov = 45f;
                w.recoilPitchKick = 3.4f;
                w.recoilYawKick = 0.8f;
                w.viewmodelKickback = 0.16f;
                w.aimAssist = 64f;
                w.range = 78f;
                w.stability = 42f;
            });

            var precision = CreateWeapon("HC_Precision_180", w =>
            {
                w.displayName = "Dusty Vow";
                w.frameName = "Precision Frame";
                w.roundsPerMinute = 180f;
                w.bodyDamage = 34f;
                w.critMultiplier = 1.5f;
                w.falloffStart = 18f;
                w.falloffEnd = 34f;
                w.magazineSize = 14;
                w.reloadSeconds = 1.6f;
                w.adsZoomFov = 50f;
                w.recoilPitchKick = 1.8f;
                w.recoilYawKick = 0.35f;
                w.viewmodelKickback = 0.09f;
                w.aimAssist = 84f;
                w.range = 48f;
                w.stability = 72f;
            });

            return new[] { adaptive, aggressive, precision };
        }

        private static WeaponData CreateWeapon(string assetName, System.Action<WeaponData> configure)
        {
            string path = "Assets/Weapons/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null) return existing; // preserve user edits

            var w = ScriptableObject.CreateInstance<WeaponData>();
            configure(w);
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        // ----- Player prefab --------------------------------------------------

        private static GameObject BuildPlayerPrefab(Mats mats, WeaponData[] weapons)
        {
            const string path = "Assets/Prefabs/Player.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            // Layer 2 (Ignore Raycast) keeps the player out of its own hitscan
            // and out of enemy line-of-sight checks.
            var go = new GameObject("Player") { layer = 2 };
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.slopeLimit = 50f;

            var controller = go.AddComponent<PlayerController>();
            var health = go.AddComponent<Health>();
            health.SetMaxHealth(190f);
            health.regenDelay = 4f;
            health.regenRate = 90f;

            // Ability stack: the router reads input, ClassLoadout fills its
            // slots from the selected class at spawn.
            go.AddComponent<PlayerAbilities>();
            go.AddComponent<ClassLoadout>();
            go.AddComponent<PlayerBody>(); // builds its primitive body in Awake

            var camGO = new GameObject("PlayerCamera") { layer = 2, tag = "MainCamera" };
            camGO.transform.SetParent(go.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            camGO.AddComponent<AudioListener>();

            var vmRoot = new GameObject("Viewmodel") { layer = 2 };
            vmRoot.transform.SetParent(camGO.transform, false);
            vmRoot.transform.localPosition = new Vector3(0.26f, -0.26f, 0.5f); // hip pose

            // One model per frame, indices matching the loadout order.
            var vmAdaptive = BuildAdaptiveViewmodel(vmRoot.transform, mats);
            var vmAggressive = BuildAggressiveViewmodel(vmRoot.transform, mats);
            var vmPrecision = BuildPrecisionViewmodel(vmRoot.transform, mats);
            vmAggressive.gameObject.SetActive(false);
            vmPrecision.gameObject.SetActive(false);

            // Magnetism cone projects from the camera so it matches the crosshair.
            var magnetism = go.AddComponent<BulletMagnetism>();
            magnetism.Configure(camGO.transform, 1 << EnemyLayer);

            var weapon = go.AddComponent<HandCannon>();
            weapon.loadout = weapons;
            weapon.player = controller;
            weapon.cam = cam;
            weapon.viewmodel = vmRoot.transform;
            weapon.viewmodels = new[] { vmAdaptive, vmAggressive, vmPrecision };
            weapon.magnetism = magnetism;

            controller.playerCamera = cam;
            controller.cameraPivot = camGO.transform;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ----- Hand cannon viewmodels (one per frame, all original primitives,
        // design language inspired by classic D2 hand cannons: flat vented top
        // rib, blade front sight, exposed spur hammer, fluted drum) -----

        private static WeaponViewmodel FinishViewmodel(GameObject root, Vector3 muzzleLocalPos)
        {
            var muzzleGO = new GameObject("MuzzlePoint") { layer = 2 };
            muzzleGO.transform.SetParent(root.transform, false);
            muzzleGO.transform.localPosition = muzzleLocalPos;

            var lightGO = new GameObject("MuzzleLight") { layer = 2 };
            lightGO.transform.SetParent(muzzleGO.transform, false);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.intensity = 3.5f;
            light.range = 6f;
            light.enabled = false;

            var vm = root.AddComponent<WeaponViewmodel>();
            vm.muzzlePoint = muzzleGO.transform;
            vm.muzzleLight = light;
            return vm;
        }

        /// <summary>140 RPM Adaptive "Last Riposte" — the Ace-inspired flagship:
        /// black frame, vented top rib, ivory grip with spade medallions, brass trim.</summary>
        private static WeaponViewmodel BuildAdaptiveViewmodel(Transform parent, Mats mats)
        {
            var root = new GameObject("VM_LastRiposte_140") { layer = 2 };
            root.transform.SetParent(parent, false);
            var t = root.transform;

            AddPart(t, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.042f, 0.08f, 0.24f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "TopStrap", new Vector3(0f, 0.052f, 0f), new Vector3(0.038f, 0.026f, 0.16f), mats.gunBlack);

            AddPart(t, PrimitiveType.Cube, "TopRib", new Vector3(0f, 0.072f, 0.10f), new Vector3(0.026f, 0.016f, 0.28f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "RibVent", new Vector3(0f, 0.0805f, 0.03f), new Vector3(0.03f, 0.005f, 0.028f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "RibVent", new Vector3(0f, 0.0805f, 0.085f), new Vector3(0.03f, 0.005f, 0.028f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "RibVent", new Vector3(0f, 0.0805f, 0.14f), new Vector3(0.03f, 0.005f, 0.028f), mats.gunSteel);

            AddPart(t, PrimitiveType.Cylinder, "BarrelShroud", new Vector3(0f, 0.045f, 0.13f), new Vector3(0.036f, 0.05f, 0.036f), mats.gunBlack, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.045f, 0.21f), new Vector3(0.028f, 0.08f, 0.028f), mats.gunSteel, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "MuzzleRing", new Vector3(0f, 0.045f, 0.278f), new Vector3(0.035f, 0.01f, 0.035f), mats.gunAccent, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "EjectorRod", new Vector3(0f, 0.004f, 0.17f), new Vector3(0.013f, 0.05f, 0.013f), mats.gunSteel, Quaternion.Euler(90f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "FrontSight", new Vector3(0f, 0.098f, 0.265f), new Vector3(0.007f, 0.024f, 0.018f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "RearSight", new Vector3(0.011f, 0.092f, -0.05f), new Vector3(0.007f, 0.015f, 0.012f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "RearSight", new Vector3(-0.011f, 0.092f, -0.05f), new Vector3(0.007f, 0.015f, 0.012f), mats.gunBlack);

            AddPart(t, PrimitiveType.Cylinder, "Drum", new Vector3(0f, 0.006f, 0f), new Vector3(0.068f, 0.026f, 0.068f), mats.gunSteel, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "DrumAxis", new Vector3(0f, 0.006f, 0f), new Vector3(0.02f, 0.03f, 0.02f), mats.gunBlack, Quaternion.Euler(90f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "Hammer", new Vector3(0f, 0.066f, -0.10f), new Vector3(0.013f, 0.04f, 0.016f), mats.gunBlack, Quaternion.Euler(-20f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "HammerSpur", new Vector3(0f, 0.09f, -0.112f), new Vector3(0.017f, 0.009f, 0.024f), mats.gunBlack, Quaternion.Euler(-50f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "GuardBottom", new Vector3(0f, -0.062f, 0f), new Vector3(0.012f, 0.008f, 0.055f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "GuardFront", new Vector3(0f, -0.046f, 0.026f), new Vector3(0.012f, 0.028f, 0.008f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "Trigger", new Vector3(0f, -0.04f, 0.002f), new Vector3(0.007f, 0.022f, 0.007f), mats.gunSteel, Quaternion.Euler(12f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.105f, -0.07f), new Vector3(0.036f, 0.125f, 0.048f), mats.gunIvory, Quaternion.Euler(18f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripSpine", new Vector3(0f, -0.098f, -0.094f), new Vector3(0.038f, 0.128f, 0.014f), mats.gunBlack, Quaternion.Euler(18f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripCap", new Vector3(0f, -0.166f, -0.088f), new Vector3(0.04f, 0.013f, 0.052f), mats.gunAccent, Quaternion.Euler(18f, 0f, 0f));
            // Diamond "spade" medallions on the ivory grip panels.
            AddPart(t, PrimitiveType.Cube, "Spade", new Vector3(0.0205f, -0.096f, -0.066f), new Vector3(0.004f, 0.014f, 0.014f), mats.gunBlack, Quaternion.Euler(45f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "Spade", new Vector3(-0.0205f, -0.096f, -0.066f), new Vector3(0.004f, 0.014f, 0.014f), mats.gunBlack, Quaternion.Euler(45f, 0f, 0f));

            return FinishViewmodel(root, new Vector3(0f, 0.045f, 0.30f));
        }

        /// <summary>120 RPM Aggressive "Iron Remit" — heavy: fat barrel with
        /// underlug, brass muzzle brake, big fluted drum, walnut grip.</summary>
        private static WeaponViewmodel BuildAggressiveViewmodel(Transform parent, Mats mats)
        {
            var root = new GameObject("VM_IronRemit_120") { layer = 2 };
            root.transform.SetParent(parent, false);
            var t = root.transform;

            AddPart(t, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0.02f), new Vector3(0.05f, 0.09f, 0.25f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "TopStrap", new Vector3(0f, 0.06f, 0.03f), new Vector3(0.046f, 0.026f, 0.22f), mats.gunMetal);
            AddPart(t, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.048f, 0.18f), new Vector3(0.042f, 0.075f, 0.042f), mats.gunMetal, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "UnderLug", new Vector3(0f, -0.002f, 0.17f), new Vector3(0.034f, 0.032f, 0.13f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "MuzzleBrake", new Vector3(0f, 0.048f, 0.272f), new Vector3(0.054f, 0.052f, 0.042f), mats.gunAccent);
            AddPart(t, PrimitiveType.Cube, "BrakeVent", new Vector3(0.029f, 0.048f, 0.272f), new Vector3(0.012f, 0.03f, 0.028f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "BrakeVent", new Vector3(-0.029f, 0.048f, 0.272f), new Vector3(0.012f, 0.03f, 0.028f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "FrontSight", new Vector3(0f, 0.098f, 0.255f), new Vector3(0.01f, 0.028f, 0.02f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "RearSight", new Vector3(0f, 0.088f, -0.055f), new Vector3(0.034f, 0.012f, 0.014f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cylinder, "Drum", new Vector3(0f, 0.004f, 0f), new Vector3(0.082f, 0.03f, 0.082f), mats.gunMetal, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "DrumBand", new Vector3(0f, 0.004f, 0.026f), new Vector3(0.084f, 0.005f, 0.084f), mats.gunAccent, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "Hammer", new Vector3(0f, 0.064f, -0.108f), new Vector3(0.016f, 0.045f, 0.02f), mats.gunBlack, Quaternion.Euler(-18f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "HammerSpur", new Vector3(0f, 0.092f, -0.122f), new Vector3(0.022f, 0.011f, 0.028f), mats.gunBlack, Quaternion.Euler(-55f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GuardBottom", new Vector3(0f, -0.068f, 0f), new Vector3(0.014f, 0.009f, 0.06f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "GuardFront", new Vector3(0f, -0.05f, 0.029f), new Vector3(0.014f, 0.032f, 0.009f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "Trigger", new Vector3(0f, -0.044f, 0.002f), new Vector3(0.008f, 0.024f, 0.008f), mats.gunSteel, Quaternion.Euler(12f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.115f, -0.078f), new Vector3(0.044f, 0.135f, 0.056f), mats.gunWood, Quaternion.Euler(22f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripStrap", new Vector3(0f, -0.106f, -0.105f), new Vector3(0.046f, 0.138f, 0.015f), mats.gunBlack, Quaternion.Euler(22f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripCap", new Vector3(0f, -0.182f, -0.098f), new Vector3(0.048f, 0.014f, 0.06f), mats.gunBlack, Quaternion.Euler(22f, 0f, 0f));
            AddPart(t, PrimitiveType.Sphere, "FrameBolt", new Vector3(0.027f, 0.01f, -0.03f), new Vector3(0.013f, 0.013f, 0.013f), mats.gunAccent);
            AddPart(t, PrimitiveType.Sphere, "FrameBolt", new Vector3(-0.027f, 0.01f, -0.03f), new Vector3(0.013f, 0.013f, 0.013f), mats.gunAccent);

            return FinishViewmodel(root, new Vector3(0f, 0.048f, 0.30f));
        }

        /// <summary>180 RPM Precision "Dusty Vow" — slim: long thin barrel,
        /// skeletonized floating rib, fiber-optic sight, two-tone steel/ivory.</summary>
        private static WeaponViewmodel BuildPrecisionViewmodel(Transform parent, Mats mats)
        {
            var root = new GameObject("VM_DustyVow_180") { layer = 2 };
            root.transform.SetParent(parent, false);
            var t = root.transform;

            AddPart(t, PrimitiveType.Cube, "Frame", new Vector3(0f, 0f, 0.01f), new Vector3(0.038f, 0.075f, 0.22f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 0.042f, 0.22f), new Vector3(0.022f, 0.10f, 0.022f), mats.gunSteel, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "MuzzleCrown", new Vector3(0f, 0.042f, 0.318f), new Vector3(0.027f, 0.008f, 0.027f), mats.gunBlack, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cylinder, "EjectorRod", new Vector3(0f, 0.006f, 0.20f), new Vector3(0.01f, 0.06f, 0.01f), mats.gunSteel, Quaternion.Euler(90f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "TopRib", new Vector3(0f, 0.066f, 0.13f), new Vector3(0.018f, 0.012f, 0.32f), mats.gunIvory);
            AddPart(t, PrimitiveType.Cube, "RibPost", new Vector3(0f, 0.052f, 0.02f), new Vector3(0.016f, 0.018f, 0.018f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "RibPost", new Vector3(0f, 0.052f, 0.24f), new Vector3(0.016f, 0.018f, 0.018f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "FrontSightPost", new Vector3(0f, 0.086f, 0.30f), new Vector3(0.006f, 0.02f, 0.012f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "FiberOptic", new Vector3(0f, 0.094f, 0.30f), new Vector3(0.006f, 0.006f, 0.01f), mats.gunSight);
            AddPart(t, PrimitiveType.Cube, "RearSight", new Vector3(0.009f, 0.08f, -0.05f), new Vector3(0.005f, 0.014f, 0.01f), mats.gunBlack);
            AddPart(t, PrimitiveType.Cube, "RearSight", new Vector3(-0.009f, 0.08f, -0.05f), new Vector3(0.005f, 0.014f, 0.01f), mats.gunBlack);

            AddPart(t, PrimitiveType.Cylinder, "Drum", new Vector3(0f, 0.004f, -0.01f), new Vector3(0.058f, 0.024f, 0.058f), mats.gunBlack, Quaternion.Euler(90f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "DrumFlute", new Vector3(0.024f, 0.004f, -0.01f), new Vector3(0.012f, 0.028f, 0.028f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "DrumFlute", new Vector3(-0.024f, 0.004f, -0.01f), new Vector3(0.012f, 0.028f, 0.028f), mats.gunSteel);

            AddPart(t, PrimitiveType.Cube, "Hammer", new Vector3(0f, 0.058f, -0.092f), new Vector3(0.011f, 0.034f, 0.014f), mats.gunSteel, Quaternion.Euler(-22f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "HammerSpur", new Vector3(0f, 0.078f, -0.103f), new Vector3(0.014f, 0.008f, 0.02f), mats.gunSteel, Quaternion.Euler(-50f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GuardBottom", new Vector3(0f, -0.058f, 0f), new Vector3(0.011f, 0.007f, 0.05f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "GuardFront", new Vector3(0f, -0.043f, 0.024f), new Vector3(0.011f, 0.026f, 0.007f), mats.gunSteel);
            AddPart(t, PrimitiveType.Cube, "Trigger", new Vector3(0f, -0.038f, 0.001f), new Vector3(0.006f, 0.02f, 0.006f), mats.gunBlack, Quaternion.Euler(12f, 0f, 0f));

            AddPart(t, PrimitiveType.Cube, "Grip", new Vector3(0f, -0.095f, -0.065f), new Vector3(0.032f, 0.115f, 0.044f), mats.gunBlack, Quaternion.Euler(16f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripPanel", new Vector3(0.018f, -0.09f, -0.06f), new Vector3(0.006f, 0.088f, 0.032f), mats.gunIvory, Quaternion.Euler(16f, 0f, 0f));
            AddPart(t, PrimitiveType.Cube, "GripPanel", new Vector3(-0.018f, -0.09f, -0.06f), new Vector3(0.006f, 0.088f, 0.032f), mats.gunIvory, Quaternion.Euler(16f, 0f, 0f));
            AddPart(t, PrimitiveType.Sphere, "LanyardRing", new Vector3(0f, -0.152f, -0.088f), new Vector3(0.014f, 0.014f, 0.014f), mats.gunAccent);

            return FinishViewmodel(root, new Vector3(0f, 0.042f, 0.335f));
        }

        private static GameObject AddPart(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 scale, Material mat) =>
            AddPart(parent, type, name, localPos, scale, mat, Quaternion.identity);

        private static GameObject AddPart(Transform parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
