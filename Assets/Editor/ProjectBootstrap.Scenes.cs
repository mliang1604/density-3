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
    public static partial class ProjectBootstrap
    {
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
    }
}
