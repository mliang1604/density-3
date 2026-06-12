using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Density3.Core;
using Density3.Encounter;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.EditorTools
{
    public static partial class ProjectBootstrap
    {
        // ----- Wave assets --------------------------------------------------------

        private static WaveData.SpawnEntry Entry(GameObject prefab, int count, string point,
            float stagger = 0.7f)
            => new WaveData.SpawnEntry { enemyPrefab = prefab, count = count, spawnPoint = point, stagger = stagger };

        /// <summary>The boss-gate reinforcement waves — baked before the Siriks
        /// prefab, which references them from its BossGate.</summary>
        private static WaveData[] BuildGateWaves(Roster roster)
        {
            EnsureFolder("Assets/Encounters");

            var g1 = CreateWave("Gate1_Reinforcements", w =>
            {
                w.displayName = "Reinforcements";
                w.startDelay = 1.5f;
                w.entries = new[]
                {
                    Entry(roster.dreg, 3, "Spawn_VaultL", 0.8f),
                    Entry(roster.shank, 3, "Spawn_WalkW", 0.6f),
                    Entry(roster.exploder, 2, "Spawn_FloorE", 1.2f)
                };
            });
            var g2 = CreateWave("Gate2_LastStand", w =>
            {
                w.displayName = "Last Stand";
                w.startDelay = 1.5f;
                w.entries = new[]
                {
                    Entry(roster.vandal, 1, "Spawn_WalkNE"),
                    Entry(roster.vandal, 1, "Spawn_WalkNW"),
                    Entry(roster.exploder, 3, "Spawn_FloorW", 1.2f),
                    Entry(roster.dreg, 2, "Spawn_VaultR", 0.8f)
                };
            });
            return new[] { g1, g2 };
        }

        /// <summary>The full Zero Hour mission: scouts from the vault flanks,
        /// then a heavy skirmish line led by a Captain, then Siriks alone —
        /// the boss IS the final wave, so the director's completion is the
        /// mission win, and his gates pour their own reinforcements.</summary>
        private static WaveData[] BuildWaves(Roster roster)
        {
            EnsureFolder("Assets/Encounters");

            var w1 = CreateWave("Wave1_Scouts", w =>
            {
                w.displayName = "Scouts";
                w.startDelay = 3f;
                w.entries = new[]
                {
                    Entry(roster.dreg, 3, "Spawn_VaultL", 0.9f),
                    Entry(roster.dreg, 3, "Spawn_VaultR", 0.9f),
                    Entry(roster.shank, 3, "Spawn_WalkE", 0.6f)
                };
            });
            var w2 = CreateWave("Wave2_Skirmishers", w =>
            {
                w.displayName = "Skirmishers";
                w.startDelay = 4f;
                w.entries = new[]
                {
                    Entry(roster.dreg, 2, "Spawn_FloorNE"),
                    Entry(roster.dreg, 2, "Spawn_FloorNW"),
                    Entry(roster.captain, 1, "Spawn_VaultR"),
                    Entry(roster.vandal, 1, "Spawn_WalkNE"),
                    Entry(roster.vandal, 1, "Spawn_WalkNW"),
                    Entry(roster.exploder, 2, "Spawn_FloorE", 1.4f)
                };
            });
            var w3 = CreateWave("Wave3_Siriks", w =>
            {
                w.displayName = "Siriks";
                w.startDelay = 5f; // the room goes quiet before the vault stirs
                w.entries = new[]
                {
                    Entry(roster.siriks, 1, "BossAnchor")
                };
            });
            return new[] { w1, w2, w3 };
        }

        private static WaveData CreateWave(string assetName, System.Action<WaveData> configure)
        {
            string path = "Assets/Encounters/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<WaveData>(path);
            if (existing != null) return existing; // preserve user edits

            var w = ScriptableObject.CreateInstance<WaveData>();
            configure(w);
            AssetDatabase.CreateAsset(w, path);
            return w;
        }

        // ----- Zero Hour: the Siriks vault room ---------------------------------

        /// <summary>
        /// Large octagonal vault chamber: the sealed vault door dominates the
        /// north wall, raised walkways ring the perimeter (one continuous loop
        /// reached by two ramps), pillars and crates break up the floor, and
        /// the lighting is fog-heavy emergency-blue. Named spawn-point
        /// transforms (10 add spawns, a boss anchor, the player start) are the
        /// contract the encounter waves are authored against.
        /// </summary>
        private static void BuildZeroHourScene(Mats mats, GameObject playerPrefab, GameObject hudPrefab,
            Roster roster)
        {
            var waves = BuildWaves(roster);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ----- Moody-vault lighting: built-in render settings only -----
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.09f, 0.11f, 0.17f);

            var lightGO = new GameObject("Directional Light");
            var sun = lightGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.65f, 0.75f, 1f);
            sun.intensity = 0.45f;
            sun.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(65f, -30f, 0f);

            var env = new GameObject("Environment").transform;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Floor";
            ground.transform.SetParent(env, false);
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80 x 80 m
            ground.GetComponent<Renderer>().sharedMaterial = mats.ground;

            // ----- Octagon shell: 8 walls at 45-degree steps, apothem 28 -----
            const float apothem = 28f;
            const float wallHeight = 14f;
            for (int i = 0; i < 8; i++)
            {
                float deg = i * 45f;
                var rot = Quaternion.Euler(0f, deg, 0f);
                Vector3 dir = rot * Vector3.forward;
                CreateBox(env, "Wall_" + i, dir * apothem + Vector3.up * (wallHeight * 0.5f),
                    new Vector3(24.5f, wallHeight, 1f), mats.wall, rot);
            }

            // Ceiling slab keeps jetpacks honest and the fog enclosed.
            CreateBox(env, "Ceiling", new Vector3(0f, wallHeight, 0f), new Vector3(62f, 0.5f, 62f), mats.wall);

            // ----- The vault door: a great sealed disc on the north wall -----
            var vault = new GameObject("VaultDoor").transform;
            vault.SetParent(env, false);
            CreateBox(env, "VaultFrame_L", new Vector3(-7.5f, 5f, 27.4f), new Vector3(1.6f, 10f, 1.4f), mats.vaultMetal);
            CreateBox(env, "VaultFrame_R", new Vector3(7.5f, 5f, 27.4f), new Vector3(1.6f, 10f, 1.4f), mats.vaultMetal);
            CreateBox(env, "VaultLintel", new Vector3(0f, 10.4f, 27.4f), new Vector3(16.6f, 1.4f, 1.4f), mats.vaultMetal);

            var door = CreateCylinder(vault, "Door", new Vector3(0f, 5.2f, 27.3f),
                new Vector3(11f, 0.55f, 11f), mats.vaultMetal);
            var glowRing = CreateCylinder(vault, "GlowRing", new Vector3(0f, 5.2f, 27.55f),
                new Vector3(11.8f, 0.25f, 11.8f), mats.vaultGlow);
            Object.DestroyImmediate(glowRing.GetComponent<Collider>()); // rim is cosmetic
            CreateCylinder(vault, "Hub", new Vector3(0f, 5.2f, 26.85f),
                new Vector3(3.2f, 0.5f, 3.2f), mats.vaultMetal);
            for (int i = 0; i < 4; i++)
            {
                var spoke = CreateBox(vault, "Spoke", new Vector3(0f, 5.2f, 26.95f),
                    new Vector3(0.5f, 9.6f, 0.3f), mats.vaultMetal,
                    Quaternion.Euler(0f, 0f, i * 45f));
                Object.DestroyImmediate(spoke.GetComponent<Collider>()); // inside the door disc
            }

            var vaultLightGO = new GameObject("VaultLight");
            vaultLightGO.transform.SetParent(vault, false);
            vaultLightGO.transform.position = new Vector3(0f, 6f, 24f);
            var vaultLight = vaultLightGO.AddComponent<Light>();
            vaultLight.type = LightType.Point;
            vaultLight.color = ElementPalette.Base(Element.Arc);
            vaultLight.range = 20f;
            vaultLight.intensity = 2.2f;

            // ----- Perimeter walkways: a continuous raised ring (the vault
            // wall is skipped — the door owns it), reached by two ramps -----
            const float walkR = 24.3f;
            const float walkY = 3.2f;
            for (int i = 1; i < 8; i++)
            {
                float deg = i * 45f;
                var rot = Quaternion.Euler(0f, deg, 0f);
                Vector3 dir = rot * Vector3.forward;
                CreateBox(env, "Walkway_" + i, dir * walkR + Vector3.up * walkY,
                    new Vector3(20.6f, 0.5f, 3.6f), mats.cover, rot);
                // Low back rail against the wall side keeps the silhouette readable.
                CreateBox(env, "WalkRail_" + i, dir * (walkR + 2.1f) + Vector3.up * (walkY + 0.55f),
                    new Vector3(20.6f, 0.6f, 0.3f), mats.vaultMetal, rot);
            }

            // Ramps up at east and west: each climbs perpendicular from the
            // floor onto the walkway's inner edge (rotated boxes, TestRange
            // ramp style). Top lands ~10cm proud — step offset absorbs it.
            CreateBox(env, "Ramp_E", new Vector3(17.2f, 1.62f, -7f), new Vector3(3.2f, 0.5f, 11.1f),
                mats.cover, Quaternion.Euler(-17.5f, 90f, 0f));
            CreateBox(env, "Ramp_W", new Vector3(-17.2f, 1.62f, -7f), new Vector3(3.2f, 0.5f, 11.1f),
                mats.cover, Quaternion.Euler(-17.5f, -90f, 0f));

            // ----- Floor cover: four heavy pillars and scattered crates -----
            for (int i = 0; i < 4; i++)
            {
                float deg = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                CreateBox(env, "Pillar", dir * 12f + Vector3.up * 4.5f,
                    new Vector3(2.6f, 9f, 2.6f), mats.wall);
            }
            CreateBox(env, "Crate", new Vector3(-5f, 0.9f, 4f), new Vector3(1.8f, 1.8f, 1.8f), mats.cover);
            CreateBox(env, "Crate", new Vector3(6f, 0.75f, -6f), new Vector3(2.4f, 1.5f, 1.5f), mats.cover);
            CreateBox(env, "Crate", new Vector3(2f, 0.9f, 12f), new Vector3(1.8f, 1.8f, 1.8f), mats.cover);
            CreateBox(env, "Crate", new Vector3(-9f, 0.75f, -12f), new Vector3(2.4f, 1.5f, 1.5f), mats.cover);
            CreateBox(env, "Crate", new Vector3(11f, 0.9f, 7f), new Vector3(1.8f, 1.8f, 1.8f), mats.cover);

            // Walkway accent lights: dim blue, one per diagonal.
            for (int i = 0; i < 4; i++)
            {
                float deg = 45f + i * 90f;
                Vector3 dir = Quaternion.Euler(0f, deg, 0f) * Vector3.forward;
                var accentGO = new GameObject("WalkwayLight_" + i);
                accentGO.transform.SetParent(env, false);
                accentGO.transform.position = dir * 22f + Vector3.up * 5.5f;
                var accent = accentGO.AddComponent<Light>();
                accent.type = LightType.Point;
                accent.color = new Color(0.35f, 0.55f, 0.95f);
                accent.range = 12f;
                accent.intensity = 1.1f;
            }

            // ----- Named spawn points: the contract WaveData is authored against -----
            var spawns = new GameObject("SpawnPoints").transform;
            void Point(string name, Vector3 pos, float yawDeg = 180f)
            {
                var p = new GameObject(name).transform;
                p.SetParent(spawns, false);
                p.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            }
            Point("PlayerStart", new Vector3(0f, 1.2f, -22f), 0f); // facing the vault
            Point("BossAnchor", new Vector3(0f, 0f, 20f));
            Point("Spawn_VaultL", new Vector3(-8f, 0f, 21f));
            Point("Spawn_VaultR", new Vector3(8f, 0f, 21f));
            Point("Spawn_FloorNE", new Vector3(14f, 0f, 12f));
            Point("Spawn_FloorNW", new Vector3(-14f, 0f, 12f));
            Point("Spawn_FloorE", new Vector3(18f, 0f, -2f));
            Point("Spawn_FloorW", new Vector3(-18f, 0f, -2f));
            Point("Spawn_WalkNE", new Vector3(17f, walkY + 0.3f, 17f));
            Point("Spawn_WalkNW", new Vector3(-17f, walkY + 0.3f, 17f));
            Point("Spawn_WalkE", new Vector3(24f, walkY + 0.3f, 0f));
            Point("Spawn_WalkW", new Vector3(-24f, walkY + 0.3f, 0f));

            // ----- Player, HUD, management -----
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 1.2f, -22f);

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(hudPrefab);

            var gmGO = new GameObject("GameManager");
            var gm = gmGO.AddComponent<GameManager>();
            gm.freeRespawn = false; // death is the mission's problem here
            gm.player = player.GetComponent<PlayerController>();
            gm.weapon = player.GetComponent<HandCannon>();
            gm.hud = hud.GetComponent<HUDController>();
            gm.gunshotRecording = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HandCannonShot.wav");
            if (gm.gunshotRecording == null)
                gm.gunshotRecording = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/HandCannonShot.mp3");

            var quit = gmGO.AddComponent<QuitHandler>();
            quit.requireHold = true;

            AddMusic(gmGO, "Assets/Audio/BattleTheme.mp3", 0.3f);

            var directorGO = new GameObject("EncounterDirector");
            var director = directorGO.AddComponent<EncounterDirector>();
            director.waves = waves;
            director.spawnRoot = spawns;

            gmGO.AddComponent<MissionController>();

            EditorSceneManager.SaveScene(scene, ZeroHourScenePath);
        }

        private static GameObject CreateCylinder(Transform parent, string name, Vector3 pos,
            Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Lying on its side: the flat faces look down the z axis (a door disc).
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
