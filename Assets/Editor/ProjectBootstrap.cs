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
    public static partial class ProjectBootstrap
    {
        private const string ScenePath = "Assets/Scenes/TestRange.unity";
        private const string TitleScenePath = "Assets/Scenes/Title.unity";
        private const string ZeroHourScenePath = "Assets/Scenes/ZeroHour.unity";
        private const int EnemyLayer = 6; // named "Enemy" in TagManager

        /// <summary>The baked enemy prefabs, as one handful.</summary>
        private class Roster
        {
            public GameObject dreg, vandal, shank, exploder, captain, siriks;
        }

        private class Mats
        {
            public Material ground, wall, cover, dummy, crit;
            public Material dregLeather, dregBone, dregCloth, dregHair, dregClaw, dregWrap, dregEye;
            public Material vandalCloth, shankBody, shankAccent, exploderEye, captainCloth;
            public Material siriksCloth, siriksGlow;
            public Material vaultMetal, vaultGlow;
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
            var enemies = BuildEnemyData();
            BuildClasses();
            var playerPrefab = BuildPlayerPrefab(mats, weapons);
            BuildDummyPrefab(mats); // asset kept for DPS testing and the M5 gallery
            var roster = new Roster
            {
                dreg = BuildDregPrefab(mats, enemies.dreg),
                vandal = BuildVandalPrefab(mats, enemies.vandal),
                shank = BuildShankPrefab(mats, enemies.shank),
                exploder = BuildExploderShankPrefab(mats, enemies.exploder),
                captain = BuildCaptainPrefab(mats, enemies.captain)
            };
            roster.siriks = BuildSiriksPrefab(mats, enemies.siriks, BuildGateWaves(roster));
            var hudPrefab = BuildHudPrefab();
            BuildScene(mats, playerPrefab, roster, hudPrefab);
            BuildZeroHourScene(mats, playerPrefab, hudPrefab, roster);
            BuildTitleScene();

            // Title first: it is the startup scene in builds.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(ZeroHourScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Density3: rebuilt — prefabs in Assets/Prefabs, materials in Assets/Materials, scenes at " + TitleScenePath + " + " + ScenePath);
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
                new[] { TitleScenePath, ScenePath, ZeroHourScenePath }, "Builds/WebGL",
                BuildTarget.WebGL, BuildOptions.None);

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

    }
}
