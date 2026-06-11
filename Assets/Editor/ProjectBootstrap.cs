using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Density3.Core;
using Density3.Enemies;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.EditorTools
{
    /// <summary>
    /// Bakes the whole project into editable assets: materials in Assets/Materials,
    /// prefabs (Player, DregEnemy, TargetDummy, HUD) in Assets/Prefabs, weapon data
    /// in Assets/Weapons, and the TestRange scene with prefab *instances* plus
    /// plain editable arena geometry.
    ///
    /// Re-running is safe: existing materials/weapons/prefabs are left untouched
    /// (your edits are preserved); only missing assets are created. The scene is
    /// always rewritten. Delete an asset to have it regenerated fresh.
    /// </summary>
    public static class ProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/TestRange.unity";
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const int EnemyLayer = 6; // named "Enemy" in TagManager

        private class Mats
        {
            public Material ground, wall, cover, dummy, crit;
            public Material dregLeather, dregBone, dregCloth, dregHair, dregClaw, dregWrap, dregEye;
            public Material gunMetal, gunAccent, gunBlack, gunIvory, gunSteel, gunWood, gunSight;
        }

        [MenuItem("Density3/Rebuild Test Range Scene")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Weapons");
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Prefabs");

            var mats = BuildMaterials();
            var weapons = BuildWeapons();
            var playerPrefab = BuildPlayerPrefab(mats, weapons);
            var dummyPrefab = BuildDummyPrefab(mats);
            var dregPrefab = BuildDregPrefab(mats);
            var hudPrefab = BuildHudPrefab();
            BuildScene(mats, playerPrefab, dummyPrefab, dregPrefab, hudPrefab);
            BuildTitleScene();

            // Title first: it is the startup scene in builds.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Density3: rebuilt — prefabs in Assets/Prefabs, materials in Assets/Materials, scenes at " + TitleScenePath + " + " + ScenePath);
        }

        // ----- Title scene ----------------------------------------------------

        /// <summary>
        /// Destiny-style title screen: the rasterized title card stretched over
        /// the whole screen, a pulsing "press Enter" prompt, and a Backspace
        /// exit hint. Backspace on the title quits immediately (QuitHandler,
        /// tap mode). Esc is left to the browser: fullscreen + pointer lock.
        /// </summary>
        private static void BuildTitleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGO = new GameObject("TitleCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.02f, 0.05f);
            camGO.AddComponent<AudioListener>();
            AddMusic(camGO, "Assets/Audio/TitleTheme.mp3", 0.45f);

            var canvasGO = new GameObject("TitleCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Background: the title card, stretched to cover the screen.
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.AddComponent<RawImage>();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/TitleBackground.png");
            if (tex != null) bg.texture = tex;
            else
            {
                bg.color = new Color(0.03f, 0.05f, 0.09f);
                Debug.LogWarning("Density3: no title art at Assets/UI/TitleBackground.png — using a flat color.");
            }
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text MakeTitleText(string name, string content, int size, Color color)
            {
                var go = new GameObject(name);
                go.transform.SetParent(canvasGO.transform, false);
                var t = go.AddComponent<Text>();
                t.font = font;
                t.fontSize = size;
                t.text = content;
                t.color = color;
                t.alignment = TextAnchor.MiddleCenter;
                t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                return t;
            }

            var cream = new Color(0.93f, 0.88f, 0.78f);

            var press = MakeTitleText("PressToPlay", "P R E S S   [ E N T E R ]   T O   P L A Y", 30, cream);
            var pressRect = press.rectTransform;
            pressRect.anchorMin = pressRect.anchorMax = new Vector2(0.5f, 0f);
            pressRect.anchoredPosition = new Vector2(0f, 150f);
            pressRect.sizeDelta = new Vector2(1200f, 44f);

            var exit = MakeTitleText("ExitHint", "[Backspace]  Exit to Desktop", 22, new Color(0.93f, 0.88f, 0.78f, 0.6f));
            var exitRect = exit.rectTransform;
            exitRect.anchorMin = exitRect.anchorMax = new Vector2(0f, 0f);
            exitRect.pivot = new Vector2(0f, 0f);
            exitRect.anchoredPosition = new Vector2(60f, 60f);
            exitRect.sizeDelta = new Vector2(400f, 30f);
            exit.alignment = TextAnchor.MiddleLeft;

            var title = canvasGO.AddComponent<TitleScreen>();
            title.gameSceneName = "TestRange";
            title.pressToPlay = press;

            var quit = canvasGO.AddComponent<QuitHandler>();
            quit.requireHold = false; // tap to quit on the title screen

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        // ----- Materials ----------------------------------------------------

        private static Mats BuildMaterials()
        {
            return new Mats
            {
                ground = MatAsset("Ground", new Color(0.16f, 0.17f, 0.2f), 0f, 0.25f),
                wall = MatAsset("Wall", new Color(0.28f, 0.3f, 0.36f), 0f, 0.3f),
                cover = MatAsset("Cover", new Color(0.45f, 0.42f, 0.36f), 0f, 0.35f),
                dummy = MatAsset("Dummy", new Color(0.75f, 0.45f, 0.15f), 0f, 0.4f),
                crit = MatAsset("CritZone", new Color(1f, 0.9f, 0.5f), 0f, 0.6f, new Color(1f, 0.85f, 0.3f) * 1.6f),

                // Eliksni palette, matched to the D2 character-render reference:
                // near-black leather body, bone-white armor plates, purple cloth,
                // maroon hair plume, tan claws, brown shin wrappings, arc-blue eyes.
                dregLeather = MatAsset("DregLeather", new Color(0.09f, 0.08f, 0.085f), 0.15f, 0.45f),
                dregBone = MatAsset("DregBoneArmor", new Color(0.85f, 0.84f, 0.80f), 0.25f, 0.55f),
                dregCloth = MatAsset("DregCloth", new Color(0.30f, 0.21f, 0.42f), 0f, 0.2f),
                dregHair = MatAsset("DregHair", new Color(0.25f, 0.10f, 0.20f), 0f, 0.3f),
                dregClaw = MatAsset("DregClaw", new Color(0.75f, 0.65f, 0.45f), 0.1f, 0.4f),
                dregWrap = MatAsset("DregWrap", new Color(0.33f, 0.27f, 0.20f), 0f, 0.25f),
                dregEye = MatAsset("DregEye", new Color(0.1f, 0.7f, 0.9f), 0f, 0.7f, new Color(0.2f, 0.8f, 1f) * 2.5f),

                gunMetal = MatAsset("GunMetal", new Color(0.18f, 0.18f, 0.2f), 0.6f, 0.65f),
                gunAccent = MatAsset("GunAccent", new Color(0.8f, 0.65f, 0.3f), 0.8f, 0.7f),
                gunBlack = MatAsset("GunBlack", new Color(0.055f, 0.055f, 0.065f), 0.55f, 0.55f),
                gunIvory = MatAsset("GunIvory", new Color(0.88f, 0.86f, 0.80f), 0.05f, 0.5f),
                gunSteel = MatAsset("GunSteel", new Color(0.55f, 0.57f, 0.60f), 0.9f, 0.7f),
                gunWood = MatAsset("GunWood", new Color(0.34f, 0.21f, 0.12f), 0f, 0.45f),
                gunSight = MatAsset("GunSight", new Color(1f, 0.4f, 0.15f), 0f, 0.6f, new Color(1f, 0.45f, 0.15f) * 2f)
            };
        }

        private static Material MatAsset(string name, Color c, float metallic, float smooth, Color? emission = null)
        {
            string path = "Assets/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing; // preserve user edits

            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smooth);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

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

        // ----- Target dummy prefab ---------------------------------------------

        private static GameObject BuildDummyPrefab(Mats mats)
        {
            const string path = "Assets/Prefabs/TargetDummy.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("TargetDummy");
            var health = root.AddComponent<Health>();
            health.SetMaxHealth(200f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = EnemyLayer;
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = mats.dummy;
            var bodyHB = body.AddComponent<Hitbox>();
            bodyHB.owner = health;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.layer = EnemyLayer;
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            head.transform.localScale = Vector3.one * 0.45f;
            head.GetComponent<Renderer>().sharedMaterial = mats.crit;
            var headHB = head.AddComponent<Hitbox>();
            headHB.owner = health;
            headHB.isCritZone = true;

            root.AddComponent<DamageFlash>();
            var rs = root.AddComponent<Respawner>();
            rs.delay = 3f;
            rs.countsAsKill = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ----- Dreg enemy prefab ------------------------------------------------

        /// <summary>
        /// Eliksni styled after the D2 reference render: white horned helmet with a
        /// maroon plume, four glowing eyes, heavy purple scarf and waist wrap, dark
        /// leathery body, FOUR full clawed arms, spiked bone pauldron, white
        /// bracers/knee guards, wrapped digitigrade shins, and clawed feet.
        /// </summary>
        private static GameObject BuildDregPrefab(Mats mats)
        {
            const string path = "Assets/Prefabs/DregEnemy.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var rootGO = new GameObject("DregEnemy") { layer = EnemyLayer };
            var root = rootGO.transform;

            var health = rootGO.AddComponent<Health>();
            health.SetMaxHealth(150f);

            var cc = rootGO.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.4f;
            cc.center = Vector3.zero;

            // The CharacterController capsule is what hitscan usually hits first,
            // so the root itself is the body Hitbox.
            var rootHB = rootGO.AddComponent<Hitbox>();
            rootHB.owner = health;

            var eyes = new List<Renderer>();
            var headRenderers = new List<Renderer>();

            Transform Bone(string name, Transform parent, Vector3 modelPos)
            {
                var b = new GameObject(name).transform;
                b.SetParent(parent, false);
                b.position = root.TransformPoint(modelPos);
                b.rotation = root.rotation;
                return b;
            }

            GameObject Part(Transform bone, string name, Vector3 modelPos, Vector3 euler,
                Vector3 scale, Material mat, PrimitiveType type)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(bone, false);
                go.transform.localScale = scale;
                go.transform.position = root.TransformPoint(modelPos);
                go.transform.rotation = root.rotation * Quaternion.Euler(euler);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                return go;
            }

            // ----- Skeleton (more upright than the old hunch) -----
            var pelvis = Bone("Pelvis", root, new Vector3(0f, -0.05f, 0.02f));
            var spine = Bone("Spine", pelvis, new Vector3(0f, 0.14f, 0.06f));
            var chest = Bone("Chest", spine, new Vector3(0f, 0.38f, 0.10f));
            var neck = Bone("Neck", chest, new Vector3(0f, 0.55f, 0.18f));
            var head = Bone("Head", neck, new Vector3(0f, 0.66f, 0.26f));
            var shL = Bone("Shoulder.L", chest, new Vector3(0.22f, 0.48f, 0.10f));
            var shR = Bone("Shoulder.R", chest, new Vector3(-0.22f, 0.48f, 0.10f));
            var uaL = Bone("UpperArm.L", shL, new Vector3(0.27f, 0.42f, 0.12f));
            var uaR = Bone("UpperArm.R", shR, new Vector3(-0.27f, 0.42f, 0.12f));
            var faL = Bone("Forearm.L", uaL, new Vector3(0.21f, 0.20f, 0.30f));
            var faR = Bone("Forearm.R", uaR, new Vector3(-0.21f, 0.20f, 0.30f));
            var loUaL = Bone("LowerArm.L", chest, new Vector3(0.19f, 0.16f, 0.10f));
            var loUaR = Bone("LowerArm.R", chest, new Vector3(-0.19f, 0.16f, 0.10f));
            var loFaL = Bone("LowerForearm.L", loUaL, new Vector3(0.15f, 0.00f, 0.26f));
            var loFaR = Bone("LowerForearm.R", loUaR, new Vector3(-0.15f, 0.00f, 0.26f));
            var thL = Bone("Thigh.L", pelvis, new Vector3(0.15f, -0.16f, 0.06f));
            var thR = Bone("Thigh.R", pelvis, new Vector3(-0.15f, -0.16f, 0.06f));
            var snL = Bone("Shin.L", thL, new Vector3(0.15f, -0.52f, 0.10f));
            var snR = Bone("Shin.R", thR, new Vector3(-0.15f, -0.52f, 0.10f));
            var ftL = Bone("Foot.L", snL, new Vector3(0.15f, -0.92f, 0.04f));
            var ftR = Bone("Foot.R", snR, new Vector3(-0.15f, -0.92f, 0.04f));

            // ----- Torso: slender dark-leather core, small ether tank -----
            Part(pelvis, "PelvisMesh", new Vector3(0f, -0.12f, 0.02f), new Vector3(5f, 0f, 0f), new Vector3(0.28f, 0.18f, 0.20f), mats.dregLeather, PrimitiveType.Cube);
            Part(spine, "Abdomen", new Vector3(0f, 0.12f, 0.06f), new Vector3(8f, 0f, 0f), new Vector3(0.24f, 0.26f, 0.18f), mats.dregLeather, PrimitiveType.Cube);
            Part(chest, "ChestMesh", new Vector3(0f, 0.40f, 0.10f), new Vector3(10f, 0f, 0f), new Vector3(0.36f, 0.30f, 0.22f), mats.dregLeather, PrimitiveType.Cube);
            Part(chest, "EtherTank", new Vector3(0f, 0.36f, -0.10f), new Vector3(14f, 0f, 0f), new Vector3(0.15f, 0.20f, 0.12f), mats.dregLeather, PrimitiveType.Capsule);

            // ----- Heavy purple scarf bunched around the neck/shoulders -----
            Part(chest, "Scarf", new Vector3(0f, 0.55f, 0.14f), Vector3.zero, new Vector3(0.36f, 0.20f, 0.32f), mats.dregCloth, PrimitiveType.Sphere);
            Part(chest, "ScarfDrape", new Vector3(0.02f, 0.38f, 0.22f), new Vector3(12f, 0f, 8f), new Vector3(0.22f, 0.28f, 0.05f), mats.dregCloth, PrimitiveType.Cube);
            Part(chest, "ScarfBack", new Vector3(0f, 0.45f, -0.04f), new Vector3(-10f, 0f, 0f), new Vector3(0.28f, 0.22f, 0.06f), mats.dregCloth, PrimitiveType.Cube);

            // ----- Head: dark face (crit zone), white horned helmet, maroon plume -----
            var headGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGO.name = "HeadCrit";
            headGO.layer = EnemyLayer;
            headGO.transform.SetParent(head, false);
            headGO.transform.localScale = new Vector3(0.24f, 0.26f, 0.28f);
            headGO.transform.position = root.TransformPoint(new Vector3(0f, 0.66f, 0.26f));
            headGO.transform.rotation = root.rotation;
            headGO.GetComponent<Renderer>().sharedMaterial = mats.dregLeather;
            headRenderers.Add(headGO.GetComponent<Renderer>());
            var headCritCollider = headGO.GetComponent<Collider>();
            var headHB = headGO.AddComponent<Hitbox>();
            headHB.owner = health;
            headHB.isCritZone = true;

            headRenderers.Add(Part(head, "Helmet", new Vector3(0f, 0.71f, 0.23f), Vector3.zero, new Vector3(0.29f, 0.26f, 0.31f), mats.dregBone, PrimitiveType.Sphere).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "HelmetBrow", new Vector3(0f, 0.70f, 0.38f), new Vector3(75f, 0f, 0f), new Vector3(0.22f, 0.06f, 0.10f), mats.dregBone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Crest.L", new Vector3(0.09f, 0.82f, 0.22f), new Vector3(-12f, 0f, 22f), new Vector3(0.035f, 0.15f, 0.08f), mats.dregBone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Crest.R", new Vector3(-0.09f, 0.82f, 0.22f), new Vector3(-12f, 0f, -22f), new Vector3(0.035f, 0.15f, 0.08f), mats.dregBone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Plume1", new Vector3(0f, 0.87f, 0.18f), new Vector3(-18f, 0f, 0f), new Vector3(0.06f, 0.16f, 0.09f), mats.dregHair, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Plume2", new Vector3(0f, 0.93f, 0.10f), new Vector3(-38f, 0f, 0f), new Vector3(0.045f, 0.13f, 0.07f), mats.dregHair, PrimitiveType.Cube).GetComponent<Renderer>());

            eyes.Add(Part(head, "Eye", new Vector3(0.07f, 0.67f, 0.385f), Vector3.zero, Vector3.one * 0.055f, mats.dregEye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(-0.07f, 0.67f, 0.385f), Vector3.zero, Vector3.one * 0.055f, mats.dregEye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(0.05f, 0.61f, 0.39f), Vector3.zero, Vector3.one * 0.035f, mats.dregEye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(-0.05f, 0.61f, 0.39f), Vector3.zero, Vector3.one * 0.035f, mats.dregEye, PrimitiveType.Sphere).GetComponent<Renderer>());
            headRenderers.AddRange(eyes);

            // ----- Upper arms: spiked bone pauldron (L), leather shoulder (R),
            // white bracers, clawed hands -----
            Part(shL, "Pauldron", new Vector3(0.26f, 0.52f, 0.08f), new Vector3(10f, 0f, 14f), new Vector3(0.17f, 0.12f, 0.18f), mats.dregBone, PrimitiveType.Sphere);
            Part(shL, "Spike1", new Vector3(0.22f, 0.60f, 0.06f), new Vector3(0f, 0f, 25f), new Vector3(0.025f, 0.09f, 0.025f), mats.dregBone, PrimitiveType.Cube);
            Part(shL, "Spike2", new Vector3(0.27f, 0.61f, 0.10f), new Vector3(0f, 0f, 5f), new Vector3(0.025f, 0.10f, 0.025f), mats.dregBone, PrimitiveType.Cube);
            Part(shL, "Spike3", new Vector3(0.31f, 0.59f, 0.04f), new Vector3(0f, 0f, -18f), new Vector3(0.025f, 0.08f, 0.025f), mats.dregBone, PrimitiveType.Cube);
            Part(shR, "Shoulder", new Vector3(-0.26f, 0.51f, 0.08f), new Vector3(10f, 0f, -14f), new Vector3(0.14f, 0.10f, 0.15f), mats.dregLeather, PrimitiveType.Sphere);

            Part(uaL, "UpperArmMesh", new Vector3(0.28f, 0.32f, 0.14f), new Vector3(28f, 0f, 14f), new Vector3(0.075f, 0.20f, 0.075f), mats.dregLeather, PrimitiveType.Cube);
            Part(uaR, "UpperArmMesh", new Vector3(-0.28f, 0.32f, 0.14f), new Vector3(28f, 0f, -14f), new Vector3(0.075f, 0.20f, 0.075f), mats.dregLeather, PrimitiveType.Cube);
            Part(faL, "ForearmMesh", new Vector3(0.215f, 0.12f, 0.36f), new Vector3(68f, 0f, 6f), new Vector3(0.065f, 0.18f, 0.065f), mats.dregLeather, PrimitiveType.Cube);
            Part(faR, "ForearmMesh", new Vector3(-0.215f, 0.12f, 0.36f), new Vector3(68f, 0f, -6f), new Vector3(0.065f, 0.18f, 0.065f), mats.dregLeather, PrimitiveType.Cube);
            Part(faL, "Bracer", new Vector3(0.21f, 0.085f, 0.40f), new Vector3(68f, 0f, 6f), new Vector3(0.085f, 0.11f, 0.085f), mats.dregBone, PrimitiveType.Cube);
            Part(faR, "Bracer", new Vector3(-0.21f, 0.085f, 0.40f), new Vector3(68f, 0f, -6f), new Vector3(0.085f, 0.11f, 0.085f), mats.dregBone, PrimitiveType.Cube);
            Part(faL, "Hand", new Vector3(0.135f, 0.045f, 0.52f), Vector3.zero, new Vector3(0.065f, 0.055f, 0.075f), mats.dregLeather, PrimitiveType.Sphere);
            Part(faR, "Hand", new Vector3(-0.135f, 0.045f, 0.52f), Vector3.zero, new Vector3(0.065f, 0.055f, 0.075f), mats.dregLeather, PrimitiveType.Sphere);
            for (int s = -1; s <= 1; s += 2)
            {
                var fa = s > 0 ? faL : faR;
                Part(fa, "Claw", new Vector3(0.115f * s, 0.02f, 0.575f), new Vector3(40f, -8f * s, 0f), new Vector3(0.014f, 0.014f, 0.055f), mats.dregClaw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.135f * s, 0.02f, 0.585f), new Vector3(40f, 0f, 0f), new Vector3(0.014f, 0.014f, 0.055f), mats.dregClaw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.155f * s, 0.02f, 0.575f), new Vector3(40f, 8f * s, 0f), new Vector3(0.014f, 0.014f, 0.055f), mats.dregClaw, PrimitiveType.Cube);
            }

            // Shock pistol in the right upper hand, arc glow at the muzzle.
            Part(faR, "ShockPistol", new Vector3(-0.135f, 0.06f, 0.58f), new Vector3(80f, 0f, 0f), new Vector3(0.06f, 0.18f, 0.09f), mats.dregLeather, PrimitiveType.Cube);
            Part(faR, "PistolMuzzle", new Vector3(-0.135f, 0.10f, 0.69f), Vector3.zero, Vector3.one * 0.05f, mats.dregEye, PrimitiveType.Sphere);

            // ----- Lower arm pair: full slender arms with clawed hands -----
            Part(loUaL, "LowerArmMesh", new Vector3(0.20f, 0.07f, 0.16f), new Vector3(42f, 0f, 16f), new Vector3(0.055f, 0.16f, 0.055f), mats.dregLeather, PrimitiveType.Cube);
            Part(loUaR, "LowerArmMesh", new Vector3(-0.20f, 0.07f, 0.16f), new Vector3(42f, 0f, -16f), new Vector3(0.055f, 0.16f, 0.055f), mats.dregLeather, PrimitiveType.Cube);
            Part(loFaL, "LowerForearmMesh", new Vector3(0.145f, -0.03f, 0.33f), new Vector3(74f, 0f, 6f), new Vector3(0.05f, 0.15f, 0.05f), mats.dregLeather, PrimitiveType.Cube);
            Part(loFaR, "LowerForearmMesh", new Vector3(-0.145f, -0.03f, 0.33f), new Vector3(74f, 0f, -6f), new Vector3(0.05f, 0.15f, 0.05f), mats.dregLeather, PrimitiveType.Cube);
            Part(loFaL, "LowerHand", new Vector3(0.105f, -0.055f, 0.43f), Vector3.zero, new Vector3(0.05f, 0.045f, 0.06f), mats.dregLeather, PrimitiveType.Sphere);
            Part(loFaR, "LowerHand", new Vector3(-0.105f, -0.055f, 0.43f), Vector3.zero, new Vector3(0.05f, 0.045f, 0.06f), mats.dregLeather, PrimitiveType.Sphere);
            for (int s = -1; s <= 1; s += 2)
            {
                var fa = s > 0 ? loFaL : loFaR;
                Part(fa, "Claw", new Vector3(0.095f * s, -0.07f, 0.475f), new Vector3(40f, -6f * s, 0f), new Vector3(0.012f, 0.012f, 0.045f), mats.dregClaw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.12f * s, -0.07f, 0.47f), new Vector3(40f, 6f * s, 0f), new Vector3(0.012f, 0.012f, 0.045f), mats.dregClaw, PrimitiveType.Cube);
            }

            // ----- Purple waist wrap with ragged loincloth panels -----
            Part(pelvis, "WaistWrap", new Vector3(0f, -0.06f, 0.02f), Vector3.zero, new Vector3(0.30f, 0.10f, 0.24f), mats.dregCloth, PrimitiveType.Cube);
            Part(pelvis, "LoinFront", new Vector3(0f, -0.32f, 0.13f), new Vector3(6f, 0f, 0f), new Vector3(0.20f, 0.42f, 0.04f), mats.dregCloth, PrimitiveType.Cube);
            Part(pelvis, "LoinBack", new Vector3(0f, -0.34f, -0.10f), new Vector3(-6f, 0f, 0f), new Vector3(0.26f, 0.46f, 0.04f), mats.dregCloth, PrimitiveType.Cube);
            Part(pelvis, "LoinSide.L", new Vector3(0.17f, -0.26f, 0f), new Vector3(0f, 0f, 5f), new Vector3(0.04f, 0.32f, 0.14f), mats.dregCloth, PrimitiveType.Cube);
            Part(pelvis, "LoinSide.R", new Vector3(-0.17f, -0.26f, 0f), new Vector3(0f, 0f, -5f), new Vector3(0.04f, 0.32f, 0.14f), mats.dregCloth, PrimitiveType.Cube);

            // ----- Digitigrade legs: knee guards, shin wrappings, clawed feet -----
            Part(thL, "ThighMesh", new Vector3(0.15f, -0.33f, 0.09f), new Vector3(16f, 0f, 0f), new Vector3(0.10f, 0.28f, 0.10f), mats.dregLeather, PrimitiveType.Cube);
            Part(thR, "ThighMesh", new Vector3(-0.15f, -0.33f, 0.09f), new Vector3(16f, 0f, 0f), new Vector3(0.10f, 0.28f, 0.10f), mats.dregLeather, PrimitiveType.Cube);
            Part(snL, "KneeGuard", new Vector3(0.15f, -0.50f, 0.17f), new Vector3(18f, 0f, 0f), new Vector3(0.10f, 0.12f, 0.05f), mats.dregBone, PrimitiveType.Cube);
            Part(snR, "KneeGuard", new Vector3(-0.15f, -0.50f, 0.17f), new Vector3(18f, 0f, 0f), new Vector3(0.10f, 0.12f, 0.05f), mats.dregBone, PrimitiveType.Cube);
            Part(snL, "ShinMesh", new Vector3(0.15f, -0.68f, 0.07f), new Vector3(-10f, 0f, 0f), new Vector3(0.08f, 0.28f, 0.08f), mats.dregLeather, PrimitiveType.Cube);
            Part(snR, "ShinMesh", new Vector3(-0.15f, -0.68f, 0.07f), new Vector3(-10f, 0f, 0f), new Vector3(0.08f, 0.28f, 0.08f), mats.dregLeather, PrimitiveType.Cube);
            for (int s = -1; s <= 1; s += 2)
            {
                var sn = s > 0 ? snL : snR;
                Part(sn, "Wrap1", new Vector3(0.15f * s, -0.62f, 0.075f), new Vector3(-10f, 0f, 4f * s), new Vector3(0.09f, 0.035f, 0.09f), mats.dregWrap, PrimitiveType.Cube);
                Part(sn, "Wrap2", new Vector3(0.15f * s, -0.71f, 0.065f), new Vector3(-10f, 0f, -4f * s), new Vector3(0.088f, 0.035f, 0.088f), mats.dregWrap, PrimitiveType.Cube);
                Part(sn, "Wrap3", new Vector3(0.15f * s, -0.79f, 0.055f), new Vector3(-10f, 0f, 3f * s), new Vector3(0.086f, 0.035f, 0.086f), mats.dregWrap, PrimitiveType.Cube);
            }
            Part(ftL, "FootMesh", new Vector3(0.15f, -0.97f, 0.09f), new Vector3(6f, 0f, 0f), new Vector3(0.095f, 0.06f, 0.20f), mats.dregLeather, PrimitiveType.Cube);
            Part(ftR, "FootMesh", new Vector3(-0.15f, -0.97f, 0.09f), new Vector3(6f, 0f, 0f), new Vector3(0.095f, 0.06f, 0.20f), mats.dregLeather, PrimitiveType.Cube);
            for (int s = -1; s <= 1; s += 2)
            {
                var ft = s > 0 ? ftL : ftR;
                Part(ft, "ToeClawIn", new Vector3(0.11f * s, -0.99f, 0.21f), new Vector3(8f, -6f * s, 0f), new Vector3(0.028f, 0.028f, 0.075f), mats.dregClaw, PrimitiveType.Cube);
                Part(ft, "ToeClawOut", new Vector3(0.19f * s, -0.99f, 0.20f), new Vector3(8f, 8f * s, 0f), new Vector3(0.028f, 0.028f, 0.075f), mats.dregClaw, PrimitiveType.Cube);
                Part(ft, "HeelClaw", new Vector3(0.15f * s, -0.98f, -0.05f), new Vector3(-10f, 0f, 0f), new Vector3(0.024f, 0.024f, 0.06f), mats.dregClaw, PrimitiveType.Cube);
            }

            // ----- Animator wiring -----
            var anim = rootGO.AddComponent<DregAnimator>();
            anim.pelvis = pelvis; anim.spine = spine; anim.chest = chest; anim.neck = neck; anim.head = head;
            anim.shoulderL = shL; anim.shoulderR = shR; anim.upperArmL = uaL; anim.upperArmR = uaR;
            anim.forearmL = faL; anim.forearmR = faR;
            anim.lowerUpperArmL = loUaL; anim.lowerUpperArmR = loUaR;
            anim.lowerForearmL = loFaL; anim.lowerForearmR = loFaR;
            anim.thighL = thL; anim.thighR = thR; anim.shinL = snL; anim.shinR = snR;
            anim.footL = ftL; anim.footR = ftR;
            anim.eyeRenderers = eyes.ToArray();

            // ----- Ragdoll: kinematic, collider-disabled bodies per bone, switched
            // to live dynamics on death by DregDeath. Bones sit on layer 2 (Ignore
            // Raycast) so corpses are skipped by the gun and enemy LOS checks.
            // The lower arm pair is not ragdolled — it rides the chest body.
            var bodies = new List<Rigidbody>();
            Rigidbody Rag(Transform bone, Rigidbody parent, float mass, int colType, Vector3 center, Vector3 size)
            {
                bone.gameObject.layer = 2;
                var rb = bone.gameObject.AddComponent<Rigidbody>();
                rb.mass = mass;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None; // enabled while dynamic only

                Collider col;
                if (colType == 0)
                {
                    var bc = bone.gameObject.AddComponent<BoxCollider>();
                    bc.center = center; bc.size = size; col = bc;
                }
                else if (colType == 2)
                {
                    var sc = bone.gameObject.AddComponent<SphereCollider>();
                    sc.center = center; sc.radius = size.x; col = sc;
                }
                else
                {
                    var cap = bone.gameObject.AddComponent<CapsuleCollider>();
                    cap.center = center; cap.radius = size.x; cap.height = size.y; cap.direction = 1; col = cap;
                }
                col.enabled = false;

                if (parent != null)
                {
                    // Limits sit wider than any live animation pose so the corpse
                    // doesn't snap when physics takes over mid-stride.
                    var j = bone.gameObject.AddComponent<CharacterJoint>();
                    j.connectedBody = parent;
                    j.enablePreprocessing = false;
                    var lt = j.lowTwistLimit; lt.limit = -30f; j.lowTwistLimit = lt;
                    var ht = j.highTwistLimit; ht.limit = 30f; j.highTwistLimit = ht;
                    var s1 = j.swing1Limit; s1.limit = 70f; j.swing1Limit = s1;
                    var s2 = j.swing2Limit; s2.limit = 70f; j.swing2Limit = s2;
                }
                bodies.Add(rb);
                return rb;
            }

            var rbPelvis = Rag(pelvis, null, 8f, 0, new Vector3(0f, -0.06f, 0f), new Vector3(0.32f, 0.26f, 0.26f));
            var rbSpine = Rag(spine, rbPelvis, 6f, 1, new Vector3(0f, 0f, 0.02f), new Vector3(0.13f, 0.30f, 0f));
            var rbChest = Rag(chest, rbSpine, 8f, 1, new Vector3(0f, 0.02f, 0f), new Vector3(0.16f, 0.34f, 0f));
            Rag(head, rbChest, 3f, 2, Vector3.zero, new Vector3(0.17f, 0f, 0f));
            var rbUaL = Rag(uaL, rbChest, 2f, 1, new Vector3(0f, -0.10f, 0.05f), new Vector3(0.06f, 0.24f, 0f));
            var rbUaR = Rag(uaR, rbChest, 2f, 1, new Vector3(0f, -0.10f, 0.05f), new Vector3(0.06f, 0.24f, 0f));
            Rag(faL, rbUaL, 1.5f, 1, new Vector3(0f, -0.10f, 0.06f), new Vector3(0.05f, 0.22f, 0f));
            Rag(faR, rbUaR, 1.5f, 1, new Vector3(0f, -0.10f, 0.06f), new Vector3(0.05f, 0.22f, 0f));
            var rbThL = Rag(thL, rbPelvis, 4f, 1, new Vector3(0f, -0.16f, 0.05f), new Vector3(0.08f, 0.32f, 0f));
            var rbThR = Rag(thR, rbPelvis, 4f, 1, new Vector3(0f, -0.16f, 0.05f), new Vector3(0.08f, 0.32f, 0f));
            Rag(snL, rbThL, 3f, 1, new Vector3(0f, -0.16f, 0f), new Vector3(0.06f, 0.34f, 0f));
            Rag(snR, rbThR, 3f, 1, new Vector3(0f, -0.16f, 0f), new Vector3(0.06f, 0.34f, 0f));

            rootGO.AddComponent<DamageFlash>();

            var death = rootGO.AddComponent<DregDeath>();
            death.bodies = bodies.ToArray();
            death.pelvisBody = rbPelvis;
            death.chestBody = rbChest;
            death.headCritCollider = headCritCollider;
            death.headRenderers = headRenderers.ToArray();
            death.headBone = head;

            rootGO.AddComponent<ChaserEnemy>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);
            return prefab;
        }

        // ----- HUD prefab -------------------------------------------------------

        private static GameObject BuildHudPrefab()
        {
            const string path = "Assets/Prefabs/HUD.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("HUD");
            var hud = go.AddComponent<HUDController>();
            hud.BuildLayout();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ----- Scene -------------------------------------------------------------

        private static void BuildScene(Mats mats, GameObject playerPrefab, GameObject dummyPrefab,
            GameObject dregPrefab, GameObject hudPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // The player prefab brings its own camera.
            var mainCam = GameObject.Find("Main Camera");
            if (mainCam != null) Object.DestroyImmediate(mainCam);

            var lightGO = GameObject.Find("Directional Light");
            if (lightGO != null)
            {
                lightGO.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                var l = lightGO.GetComponent<Light>();
                l.color = new Color(1f, 0.96f, 0.88f);
                l.intensity = 1.15f;
                l.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = AmbientMode.Skybox;

            BuildArena(mats);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 1.2f, -45f);

            var targets = new GameObject("TargetRange").transform;
            PlaceDummy(dummyPrefab, targets, new Vector3(-9f, 0f, -35f));  // 10 m
            PlaceDummy(dummyPrefab, targets, new Vector3(-3f, 0f, -25f));  // 20 m
            PlaceDummy(dummyPrefab, targets, new Vector3(3f, 0f, -10f));   // 35 m
            PlaceDummy(dummyPrefab, targets, new Vector3(9f, 0f, 5f));     // 50 m

            var mover = PlaceDummy(dummyPrefab, targets, new Vector3(16f, 0f, -25f));
            mover.name = "TargetDummy_Moving";
            var pp = mover.AddComponent<PingPongMover>();
            pp.pointA = new Vector3(12f, 0f, -25f);
            pp.pointB = new Vector3(24f, 0f, -25f);
            pp.speed = 5f;

            var enemies = new GameObject("Enemies").transform;
            Vector3[] posts =
            {
                new Vector3(-20f, 0f, 12f),
                new Vector3(2f, 0f, 30f),
                new Vector3(22f, 0f, 15f)
            };
            foreach (var p in posts)
            {
                var dreg = (GameObject)PrefabUtility.InstantiatePrefab(dregPrefab);
                dreg.transform.SetParent(enemies, false);
                dreg.transform.position = p + Vector3.up * 1.05f;
            }

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);

            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.player = player.GetComponent<PlayerController>();
            gm.weapon = player.GetComponent<HandCannon>();
            gm.hud = hud.GetComponent<HUDController>();
            gm.gunshotRecording = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HandCannonShot.wav");
            if (gm.gunshotRecording == null)
                gm.gunshotRecording = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HandCannonShot.mp3");

            // Global exit: hold Backspace in gameplay (Esc belongs to the browser).
            var quit = gmGO.AddComponent<QuitHandler>();
            quit.requireHold = true;

            AddMusic(gmGO, "Assets/Audio/BattleTheme.mp3", 0.3f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Looping 2D music source; the clip is Inspector-swappable.</summary>
        private static void AddMusic(GameObject host, string clipPath, float volume)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogWarning("Density3: no music clip at " + clipPath);
                return;
            }
            var src = host.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.volume = volume;
            src.spatialBlend = 0f;
            src.priority = 64; // music should not be stolen by SFX voices
        }

        private static GameObject PlaceDummy(GameObject prefab, Transform parent, Vector3 pos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go;
        }

        private static void BuildArena(Mats mats)
        {
            var env = new GameObject("Environment").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(env, false);
            ground.transform.localScale = new Vector3(12f, 1f, 12f); // 120 x 120 m
            ground.GetComponent<Renderer>().sharedMaterial = mats.ground;

            // Tall enough that a full 7 m triple-jump stack can't clear them.
            const float half = 60f;
            CreateBox(env, "Wall_N", new Vector3(0f, 5f, half), new Vector3(120f, 10f, 1f), mats.wall);
            CreateBox(env, "Wall_S", new Vector3(0f, 5f, -half), new Vector3(120f, 10f, 1f), mats.wall);
            CreateBox(env, "Wall_E", new Vector3(half, 5f, 0f), new Vector3(1f, 10f, 120f), mats.wall);
            CreateBox(env, "Wall_W", new Vector3(-half, 5f, 0f), new Vector3(1f, 10f, 120f), mats.wall);

            CreateBox(env, "Cover", new Vector3(-8f, 0.75f, -20f), new Vector3(3f, 1.5f, 1f), mats.cover);
            CreateBox(env, "Cover", new Vector3(7f, 0.75f, -12f), new Vector3(3f, 1.5f, 1f), mats.cover);
            CreateBox(env, "Cover", new Vector3(-15f, 1f, 5f), new Vector3(2f, 2f, 2f), mats.cover);
            CreateBox(env, "Cover", new Vector3(14f, 1f, 18f), new Vector3(2f, 2f, 2f), mats.cover);
            CreateBox(env, "Cover", new Vector3(0f, 1f, 8f), new Vector3(4f, 2f, 1.5f), mats.cover);
            CreateBox(env, "Pillar", new Vector3(-25f, 3f, -5f), new Vector3(2f, 6f, 2f), mats.wall);
            CreateBox(env, "Pillar", new Vector3(25f, 3f, -5f), new Vector3(2f, 6f, 2f), mats.wall);
            CreateBox(env, "Platform", new Vector3(-30f, 1.25f, 25f), new Vector3(8f, 2.5f, 8f), mats.cover);
            CreateBox(env, "Ramp", new Vector3(-30f, 0.6f, 17f), new Vector3(4f, 0.5f, 10f), mats.cover,
                Quaternion.Euler(-14f, 0f, 0f));

            // Distance markers for falloff testing (player spawns at z = -45).
            float[] dists = { 10f, 20f, 35f, 50f };
            foreach (float d in dists)
            {
                CreateBox(env, "RangeLine", new Vector3(0f, 0.011f, -45f + d), new Vector3(40f, 0.02f, 0.15f), mats.crit);
                CreateWorldLabel(env, new Vector3(-11f, 0.6f, -45f + d), d + "m");
            }
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
            => CreateBox(parent, name, pos, scale, mat, Quaternion.identity);

        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale,
            Material mat, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void CreateWorldLabel(Transform parent, Vector3 pos, string text)
        {
            var go = new GameObject("Label_" + text);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 48;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.9f, 0.6f, 0.9f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ----- WebGL build ---------------------------------------------------------

        /// <summary>
        /// Builds the WASM/WebGL player to Builds/WebGL, configured for static
        /// hosts like GitHub Pages (decompression fallback, so no special HTTP
        /// headers are needed). Also force-includes the shaders that FX/SFX code
        /// creates at runtime via Shader.Find, which would otherwise be stripped.
        /// </summary>
        [MenuItem("Density3/Build WebGL")]
        public static void BuildWebGL()
        {
            EnsureAlwaysIncludedShaders("Sprites/Default", "Legacy Shaders/Particles/Additive");

            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.runInBackground = true;

            var report = BuildPipeline.BuildPlayer(
                new[] { TitleScenePath, ScenePath }, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Density3: WebGL build succeeded — {report.summary.totalSize / (1024 * 1024)} MB at Builds/WebGL");
            }
            else
            {
                Debug.LogError($"Density3: WebGL build {report.summary.result} — {report.summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Adds shaders to GraphicsSettings' Always Included list so
        /// runtime Shader.Find calls survive build stripping.</summary>
        private static void EnsureAlwaysIncludedShaders(params string[] shaderNames)
        {
            var settings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");

            foreach (string name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                {
                    Debug.LogWarning("Density3: shader not found: " + name);
                    continue;
                }

                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }

                if (!present)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                }
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        // ----- Audio import: trim one shot out of a range recording ---------------

        /// <summary>
        /// Cuts the first gunshot out of Assets/Audio/RevolverRaw.mp3 (a multi-shot
        /// range recording), saves it as Assets/Audio/HandCannonShot.wav, removes
        /// the raw file and any old placeholder clip, and rebuilds the scene so the
        /// GameManager points at the new clip.
        /// </summary>
        [MenuItem("Density3/Import Trimmed Revolver Recording")]
        public static void ImportRevolverRecording()
        {
            const string rawPath = "Assets/Audio/RevolverRaw.mp3";
            const string outPath = "Assets/Audio/HandCannonShot.wav";

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(rawPath);
            if (clip == null)
            {
                Debug.LogError("Density3: no clip at " + rawPath);
                return;
            }

            int channels = clip.channels;
            var interleaved = new float[clip.samples * channels];
            if (!clip.GetData(interleaved, 0))
            {
                Debug.LogError("Density3: couldn't read samples from " + rawPath);
                return;
            }

            // Mono mixdown.
            int n = clip.samples;
            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
                mono[i] = sum / channels;
            }

            int sr = clip.frequency;
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(mono[i]));
            if (peak < 0.01f)
            {
                Debug.LogError("Density3: recording appears to be silent.");
                return;
            }

            // First transient = shot onset; start a hair before it.
            int onset = 0;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(mono[i]) > peak * 0.3f) { onset = i; break; }
            int start = Mathf.Max(0, onset - sr * 5 / 1000);

            // End before the next shot if one exists, else cap at 1.5 s of tail.
            int nextOnset = -1;
            for (int i = onset + (int)(0.3f * sr); i < n; i++)
                if (Mathf.Abs(mono[i]) > peak * 0.45f) { nextOnset = i; break; }
            int endLimit = nextOnset > 0 ? nextOnset - (int)(0.06f * sr) : n;
            int end = Mathf.Min(start + (int)(1.5f * sr), endLimit);
            if (end <= start + sr / 10)
            {
                Debug.LogError("Density3: trim window came out too small — onset detection failed.");
                return;
            }

            int len = end - start;
            var cut = new float[len];
            System.Array.Copy(mono, start, cut, 0, len);

            // Short fade-in (click guard) and fade-out (clean tail).
            int fadeIn = Mathf.Min(64, len / 10);
            for (int i = 0; i < fadeIn; i++) cut[i] *= (float)i / fadeIn;
            int fadeOut = Mathf.Min((int)(0.08f * sr), len / 4);
            for (int i = 0; i < fadeOut; i++) cut[len - 1 - i] *= (float)i / fadeOut;

            // Normalize to a consistent game volume (quiet field recordings
            // otherwise get lost under the synthesized SFX).
            float cutPeak = 0f;
            for (int i = 0; i < len; i++) cutPeak = Mathf.Max(cutPeak, Mathf.Abs(cut[i]));
            if (cutPeak > 0.001f)
            {
                float norm = 0.9f / cutPeak;
                for (int i = 0; i < len; i++) cut[i] *= norm;
            }

            WriteWav(System.IO.Path.GetFullPath(outPath), cut, sr);
            AssetDatabase.DeleteAsset("Assets/Audio/HandCannonShot.mp3");
            AssetDatabase.DeleteAsset(rawPath);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"Density3: trimmed single shot — onset {(float)onset / sr:F2}s, length {(float)len / sr:F2}s -> {outPath}");

            BuildAll(); // rewire the scene's GameManager to the new clip
        }

        private static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var bw = new System.IO.BinaryWriter(fs))
            {
                int dataLen = samples.Length * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataLen);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);       // PCM
                bw.Write((short)1);       // mono
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2); // byte rate
                bw.Write((short)2);       // block align
                bw.Write((short)16);      // bits per sample
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataLen);
                foreach (float s in samples)
                    bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32760f));
            }
        }
    }
}
