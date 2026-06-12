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
        // ----- Enemy data assets ------------------------------------------------

        private class EnemyAssets
        {
            public EnemyData dreg;
            public EnemyData vandal;
            public EnemyData shank;
            public EnemyData exploder;
            public EnemyData captain;
        }

        /// <summary>Bakes one EnemyData asset per archetype into Assets/Enemies —
        /// the roster's whole balance table, editable without touching prefabs.</summary>
        private static EnemyAssets BuildEnemyData()
        {
            EnsureFolder("Assets/Enemies");
            return new EnemyAssets
            {
                // EnemyData's class defaults ARE the classic Dreg tuning.
                dreg = CreateEnemyData("DregData", d => d.displayName = "Dreg"),
                vandal = CreateEnemyData("VandalData", VandalEnemy.Configure),
                shank = CreateEnemyData("ShankData", ShankEnemy.Configure),
                exploder = CreateEnemyData("ExploderShankData", ExploderShankEnemy.Configure),
                captain = CreateEnemyData("CaptainData", CaptainEnemy.Configure)
            };
        }

        private static EnemyData CreateEnemyData(string assetName, System.Action<EnemyData> configure)
        {
            string path = "Assets/Enemies/" + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (existing != null) return existing; // preserve user edits

            var d = ScriptableObject.CreateInstance<EnemyData>();
            configure(d);
            AssetDatabase.CreateAsset(d, path);
            return d;
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

        // ----- Eliksni rig (shared by Dreg, Vandal, Captain — and later Siriks) --

        private enum EliksniWeapon { ShockPistol, WireRifle }

        /// <summary>Parameters for one Eliksni build: uniform scale, palette,
        /// weapon prop, brain type, and the balance asset. Defaults model the
        /// classic Dreg; the palette fields have no defaults so every spec
        /// states its colors explicitly.</summary>
        private class EliksniSpec
        {
            public string path;
            public string name;
            public float scale = 1f;
            public EliksniWeapon weapon = EliksniWeapon.ShockPistol;
            public System.Type brain = typeof(ChaserEnemy);
            public EnemyData data;
            public Material leather, bone, cloth, hair, claw, wrap, eye;
            public bool regalia;   // Captain extras: twin pauldrons, chest plate, horns
            public float arcShield; // > 0 adds an EnergyShield with this pool
        }

        /// <summary>
        /// Eliksni styled after the D2 reference render: white horned helmet with a
        /// maroon plume, four glowing eyes, heavy scarf and waist wrap, dark
        /// leathery body, FOUR full clawed arms, spiked bone pauldron, white
        /// bracers/knee guards, wrapped digitigrade shins, and clawed feet.
        /// At scale 1 with the Dreg palette this reproduces the original Dreg
        /// build exactly; Vandal/Captain pass a taller scale, their own cloth,
        /// and a different weapon prop.
        /// </summary>
        private static GameObject BuildEliksniPrefab(Mats mats, EliksniSpec spec)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(spec.path);
            if (existing != null) return existing;

            float s = spec.scale;

            var rootGO = new GameObject(spec.name) { layer = EnemyLayer };
            var root = rootGO.transform;

            var health = rootGO.AddComponent<Health>();
            health.SetMaxHealth(spec.data != null ? spec.data.maxHealth : 150f);

            var cc = rootGO.AddComponent<CharacterController>();
            cc.height = 2f * s;
            cc.radius = 0.4f * s;
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
                b.position = root.TransformPoint(modelPos * s);
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
                go.transform.localScale = scale * s;
                go.transform.position = root.TransformPoint(modelPos * s);
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
            Part(pelvis, "PelvisMesh", new Vector3(0f, -0.12f, 0.02f), new Vector3(5f, 0f, 0f), new Vector3(0.28f, 0.18f, 0.20f), spec.leather, PrimitiveType.Cube);
            Part(spine, "Abdomen", new Vector3(0f, 0.12f, 0.06f), new Vector3(8f, 0f, 0f), new Vector3(0.24f, 0.26f, 0.18f), spec.leather, PrimitiveType.Cube);
            Part(chest, "ChestMesh", new Vector3(0f, 0.40f, 0.10f), new Vector3(10f, 0f, 0f), new Vector3(0.36f, 0.30f, 0.22f), spec.leather, PrimitiveType.Cube);
            Part(chest, "EtherTank", new Vector3(0f, 0.36f, -0.10f), new Vector3(14f, 0f, 0f), new Vector3(0.15f, 0.20f, 0.12f), spec.leather, PrimitiveType.Capsule);

            // ----- Heavy scarf bunched around the neck/shoulders -----
            Part(chest, "Scarf", new Vector3(0f, 0.55f, 0.14f), Vector3.zero, new Vector3(0.36f, 0.20f, 0.32f), spec.cloth, PrimitiveType.Sphere);
            Part(chest, "ScarfDrape", new Vector3(0.02f, 0.38f, 0.22f), new Vector3(12f, 0f, 8f), new Vector3(0.22f, 0.28f, 0.05f), spec.cloth, PrimitiveType.Cube);
            Part(chest, "ScarfBack", new Vector3(0f, 0.45f, -0.04f), new Vector3(-10f, 0f, 0f), new Vector3(0.28f, 0.22f, 0.06f), spec.cloth, PrimitiveType.Cube);

            // ----- Head: dark face (crit zone), white horned helmet, maroon plume -----
            var headGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            headGO.name = "HeadCrit";
            headGO.layer = EnemyLayer;
            headGO.transform.SetParent(head, false);
            headGO.transform.localScale = new Vector3(0.24f, 0.26f, 0.28f) * s;
            headGO.transform.position = root.TransformPoint(new Vector3(0f, 0.66f, 0.26f) * s);
            headGO.transform.rotation = root.rotation;
            headGO.GetComponent<Renderer>().sharedMaterial = spec.leather;
            headRenderers.Add(headGO.GetComponent<Renderer>());
            var headCritCollider = headGO.GetComponent<Collider>();
            var headHB = headGO.AddComponent<Hitbox>();
            headHB.owner = health;
            headHB.isCritZone = true;

            headRenderers.Add(Part(head, "Helmet", new Vector3(0f, 0.71f, 0.23f), Vector3.zero, new Vector3(0.29f, 0.26f, 0.31f), spec.bone, PrimitiveType.Sphere).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "HelmetBrow", new Vector3(0f, 0.70f, 0.38f), new Vector3(75f, 0f, 0f), new Vector3(0.22f, 0.06f, 0.10f), spec.bone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Crest.L", new Vector3(0.09f, 0.82f, 0.22f), new Vector3(-12f, 0f, 22f), new Vector3(0.035f, 0.15f, 0.08f), spec.bone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Crest.R", new Vector3(-0.09f, 0.82f, 0.22f), new Vector3(-12f, 0f, -22f), new Vector3(0.035f, 0.15f, 0.08f), spec.bone, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Plume1", new Vector3(0f, 0.87f, 0.18f), new Vector3(-18f, 0f, 0f), new Vector3(0.06f, 0.16f, 0.09f), spec.hair, PrimitiveType.Cube).GetComponent<Renderer>());
            headRenderers.Add(Part(head, "Plume2", new Vector3(0f, 0.93f, 0.10f), new Vector3(-38f, 0f, 0f), new Vector3(0.045f, 0.13f, 0.07f), spec.hair, PrimitiveType.Cube).GetComponent<Renderer>());

            eyes.Add(Part(head, "Eye", new Vector3(0.07f, 0.67f, 0.385f), Vector3.zero, Vector3.one * 0.055f, spec.eye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(-0.07f, 0.67f, 0.385f), Vector3.zero, Vector3.one * 0.055f, spec.eye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(0.05f, 0.61f, 0.39f), Vector3.zero, Vector3.one * 0.035f, spec.eye, PrimitiveType.Sphere).GetComponent<Renderer>());
            eyes.Add(Part(head, "Eye", new Vector3(-0.05f, 0.61f, 0.39f), Vector3.zero, Vector3.one * 0.035f, spec.eye, PrimitiveType.Sphere).GetComponent<Renderer>());
            headRenderers.AddRange(eyes);

            // ----- Upper arms: spiked bone pauldron (L), leather shoulder (R),
            // white bracers, clawed hands. Regalia upgrades the right shoulder
            // to a matching spiked pauldron and adds a chest plate and horns. -----
            Part(shL, "Pauldron", new Vector3(0.26f, 0.52f, 0.08f), new Vector3(10f, 0f, 14f), new Vector3(0.17f, 0.12f, 0.18f), spec.bone, PrimitiveType.Sphere);
            Part(shL, "Spike1", new Vector3(0.22f, 0.60f, 0.06f), new Vector3(0f, 0f, 25f), new Vector3(0.025f, 0.09f, 0.025f), spec.bone, PrimitiveType.Cube);
            Part(shL, "Spike2", new Vector3(0.27f, 0.61f, 0.10f), new Vector3(0f, 0f, 5f), new Vector3(0.025f, 0.10f, 0.025f), spec.bone, PrimitiveType.Cube);
            Part(shL, "Spike3", new Vector3(0.31f, 0.59f, 0.04f), new Vector3(0f, 0f, -18f), new Vector3(0.025f, 0.08f, 0.025f), spec.bone, PrimitiveType.Cube);
            if (spec.regalia)
            {
                Part(shR, "Pauldron", new Vector3(-0.26f, 0.52f, 0.08f), new Vector3(10f, 0f, -14f), new Vector3(0.17f, 0.12f, 0.18f), spec.bone, PrimitiveType.Sphere);
                Part(shR, "Spike1", new Vector3(-0.22f, 0.60f, 0.06f), new Vector3(0f, 0f, -25f), new Vector3(0.025f, 0.09f, 0.025f), spec.bone, PrimitiveType.Cube);
                Part(shR, "Spike2", new Vector3(-0.27f, 0.61f, 0.10f), new Vector3(0f, 0f, -5f), new Vector3(0.025f, 0.10f, 0.025f), spec.bone, PrimitiveType.Cube);
                Part(shR, "Spike3", new Vector3(-0.31f, 0.59f, 0.04f), new Vector3(0f, 0f, 18f), new Vector3(0.025f, 0.08f, 0.025f), spec.bone, PrimitiveType.Cube);
                Part(chest, "ChestPlate", new Vector3(0f, 0.42f, 0.22f), new Vector3(10f, 0f, 0f), new Vector3(0.30f, 0.24f, 0.05f), spec.bone, PrimitiveType.Cube);
                headRenderers.Add(Part(head, "Horn.L", new Vector3(0.13f, 0.88f, 0.20f), new Vector3(-20f, 0f, 32f), new Vector3(0.035f, 0.24f, 0.06f), spec.bone, PrimitiveType.Cube).GetComponent<Renderer>());
                headRenderers.Add(Part(head, "Horn.R", new Vector3(-0.13f, 0.88f, 0.20f), new Vector3(-20f, 0f, -32f), new Vector3(0.035f, 0.24f, 0.06f), spec.bone, PrimitiveType.Cube).GetComponent<Renderer>());
            }
            else
            {
                Part(shR, "Shoulder", new Vector3(-0.26f, 0.51f, 0.08f), new Vector3(10f, 0f, -14f), new Vector3(0.14f, 0.10f, 0.15f), spec.leather, PrimitiveType.Sphere);
            }

            Part(uaL, "UpperArmMesh", new Vector3(0.28f, 0.32f, 0.14f), new Vector3(28f, 0f, 14f), new Vector3(0.075f, 0.20f, 0.075f), spec.leather, PrimitiveType.Cube);
            Part(uaR, "UpperArmMesh", new Vector3(-0.28f, 0.32f, 0.14f), new Vector3(28f, 0f, -14f), new Vector3(0.075f, 0.20f, 0.075f), spec.leather, PrimitiveType.Cube);
            Part(faL, "ForearmMesh", new Vector3(0.215f, 0.12f, 0.36f), new Vector3(68f, 0f, 6f), new Vector3(0.065f, 0.18f, 0.065f), spec.leather, PrimitiveType.Cube);
            Part(faR, "ForearmMesh", new Vector3(-0.215f, 0.12f, 0.36f), new Vector3(68f, 0f, -6f), new Vector3(0.065f, 0.18f, 0.065f), spec.leather, PrimitiveType.Cube);
            Part(faL, "Bracer", new Vector3(0.21f, 0.085f, 0.40f), new Vector3(68f, 0f, 6f), new Vector3(0.085f, 0.11f, 0.085f), spec.bone, PrimitiveType.Cube);
            Part(faR, "Bracer", new Vector3(-0.21f, 0.085f, 0.40f), new Vector3(68f, 0f, -6f), new Vector3(0.085f, 0.11f, 0.085f), spec.bone, PrimitiveType.Cube);
            Part(faL, "Hand", new Vector3(0.135f, 0.045f, 0.52f), Vector3.zero, new Vector3(0.065f, 0.055f, 0.075f), spec.leather, PrimitiveType.Sphere);
            Part(faR, "Hand", new Vector3(-0.135f, 0.045f, 0.52f), Vector3.zero, new Vector3(0.065f, 0.055f, 0.075f), spec.leather, PrimitiveType.Sphere);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var fa = sign > 0 ? faL : faR;
                Part(fa, "Claw", new Vector3(0.115f * sign, 0.02f, 0.575f), new Vector3(40f, -8f * sign, 0f), new Vector3(0.014f, 0.014f, 0.055f), spec.claw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.135f * sign, 0.02f, 0.585f), new Vector3(40f, 0f, 0f), new Vector3(0.014f, 0.014f, 0.055f), spec.claw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.155f * sign, 0.02f, 0.575f), new Vector3(40f, 8f * sign, 0f), new Vector3(0.014f, 0.014f, 0.055f), spec.claw, PrimitiveType.Cube);
            }

            // ----- Weapon prop in the right upper hand -----
            Transform muzzleTip = null;
            if (spec.weapon == EliksniWeapon.ShockPistol)
            {
                // Shock pistol, arc glow at the muzzle.
                Part(faR, "ShockPistol", new Vector3(-0.135f, 0.06f, 0.58f), new Vector3(80f, 0f, 0f), new Vector3(0.06f, 0.18f, 0.09f), spec.leather, PrimitiveType.Cube);
                Part(faR, "PistolMuzzle", new Vector3(-0.135f, 0.10f, 0.69f), Vector3.zero, Vector3.one * 0.05f, spec.eye, PrimitiveType.Sphere);
            }
            else
            {
                // Long wire rifle laid along the aim line: stock, receiver,
                // thin barrel, top-mounted scope, glowing emitter tip.
                Part(faR, "RifleStock", new Vector3(-0.135f, 0.055f, 0.45f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.10f, 0.07f), spec.leather, PrimitiveType.Cube);
                Part(faR, "RifleReceiver", new Vector3(-0.135f, 0.085f, 0.60f), new Vector3(90f, 0f, 0f), new Vector3(0.06f, 0.18f, 0.085f), mats.gunBlack, PrimitiveType.Cube);
                Part(faR, "RifleBarrel", new Vector3(-0.135f, 0.085f, 0.95f), new Vector3(90f, 0f, 0f), new Vector3(0.022f, 0.26f, 0.022f), mats.gunSteel, PrimitiveType.Cylinder);
                Part(faR, "RifleScope", new Vector3(-0.135f, 0.14f, 0.58f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.06f, 0.03f), mats.gunBlack, PrimitiveType.Cylinder);
                Part(faR, "RifleCoil1", new Vector3(-0.135f, 0.085f, 0.74f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.04f), spec.eye, PrimitiveType.Cylinder);
                Part(faR, "RifleCoil2", new Vector3(-0.135f, 0.085f, 0.84f), new Vector3(90f, 0f, 0f), new Vector3(0.04f, 0.012f, 0.04f), spec.eye, PrimitiveType.Cylinder);
                Part(faR, "RifleMuzzle", new Vector3(-0.135f, 0.085f, 1.21f), Vector3.zero, Vector3.one * 0.035f, spec.eye, PrimitiveType.Sphere);

                muzzleTip = new GameObject("MuzzleTip").transform;
                muzzleTip.SetParent(faR, false);
                muzzleTip.position = root.TransformPoint(new Vector3(-0.135f, 0.085f, 1.23f) * s);
                muzzleTip.rotation = root.rotation;
            }

            // ----- Lower arm pair: full slender arms with clawed hands -----
            Part(loUaL, "LowerArmMesh", new Vector3(0.20f, 0.07f, 0.16f), new Vector3(42f, 0f, 16f), new Vector3(0.055f, 0.16f, 0.055f), spec.leather, PrimitiveType.Cube);
            Part(loUaR, "LowerArmMesh", new Vector3(-0.20f, 0.07f, 0.16f), new Vector3(42f, 0f, -16f), new Vector3(0.055f, 0.16f, 0.055f), spec.leather, PrimitiveType.Cube);
            Part(loFaL, "LowerForearmMesh", new Vector3(0.145f, -0.03f, 0.33f), new Vector3(74f, 0f, 6f), new Vector3(0.05f, 0.15f, 0.05f), spec.leather, PrimitiveType.Cube);
            Part(loFaR, "LowerForearmMesh", new Vector3(-0.145f, -0.03f, 0.33f), new Vector3(74f, 0f, -6f), new Vector3(0.05f, 0.15f, 0.05f), spec.leather, PrimitiveType.Cube);
            Part(loFaL, "LowerHand", new Vector3(0.105f, -0.055f, 0.43f), Vector3.zero, new Vector3(0.05f, 0.045f, 0.06f), spec.leather, PrimitiveType.Sphere);
            Part(loFaR, "LowerHand", new Vector3(-0.105f, -0.055f, 0.43f), Vector3.zero, new Vector3(0.05f, 0.045f, 0.06f), spec.leather, PrimitiveType.Sphere);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var fa = sign > 0 ? loFaL : loFaR;
                Part(fa, "Claw", new Vector3(0.095f * sign, -0.07f, 0.475f), new Vector3(40f, -6f * sign, 0f), new Vector3(0.012f, 0.012f, 0.045f), spec.claw, PrimitiveType.Cube);
                Part(fa, "Claw", new Vector3(0.12f * sign, -0.07f, 0.47f), new Vector3(40f, 6f * sign, 0f), new Vector3(0.012f, 0.012f, 0.045f), spec.claw, PrimitiveType.Cube);
            }

            // ----- Waist wrap with ragged loincloth panels -----
            Part(pelvis, "WaistWrap", new Vector3(0f, -0.06f, 0.02f), Vector3.zero, new Vector3(0.30f, 0.10f, 0.24f), spec.cloth, PrimitiveType.Cube);
            Part(pelvis, "LoinFront", new Vector3(0f, -0.32f, 0.13f), new Vector3(6f, 0f, 0f), new Vector3(0.20f, 0.42f, 0.04f), spec.cloth, PrimitiveType.Cube);
            Part(pelvis, "LoinBack", new Vector3(0f, -0.34f, -0.10f), new Vector3(-6f, 0f, 0f), new Vector3(0.26f, 0.46f, 0.04f), spec.cloth, PrimitiveType.Cube);
            Part(pelvis, "LoinSide.L", new Vector3(0.17f, -0.26f, 0f), new Vector3(0f, 0f, 5f), new Vector3(0.04f, 0.32f, 0.14f), spec.cloth, PrimitiveType.Cube);
            Part(pelvis, "LoinSide.R", new Vector3(-0.17f, -0.26f, 0f), new Vector3(0f, 0f, -5f), new Vector3(0.04f, 0.32f, 0.14f), spec.cloth, PrimitiveType.Cube);

            // ----- Digitigrade legs: knee guards, shin wrappings, clawed feet -----
            Part(thL, "ThighMesh", new Vector3(0.15f, -0.33f, 0.09f), new Vector3(16f, 0f, 0f), new Vector3(0.10f, 0.28f, 0.10f), spec.leather, PrimitiveType.Cube);
            Part(thR, "ThighMesh", new Vector3(-0.15f, -0.33f, 0.09f), new Vector3(16f, 0f, 0f), new Vector3(0.10f, 0.28f, 0.10f), spec.leather, PrimitiveType.Cube);
            Part(snL, "KneeGuard", new Vector3(0.15f, -0.50f, 0.17f), new Vector3(18f, 0f, 0f), new Vector3(0.10f, 0.12f, 0.05f), spec.bone, PrimitiveType.Cube);
            Part(snR, "KneeGuard", new Vector3(-0.15f, -0.50f, 0.17f), new Vector3(18f, 0f, 0f), new Vector3(0.10f, 0.12f, 0.05f), spec.bone, PrimitiveType.Cube);
            Part(snL, "ShinMesh", new Vector3(0.15f, -0.68f, 0.07f), new Vector3(-10f, 0f, 0f), new Vector3(0.08f, 0.28f, 0.08f), spec.leather, PrimitiveType.Cube);
            Part(snR, "ShinMesh", new Vector3(-0.15f, -0.68f, 0.07f), new Vector3(-10f, 0f, 0f), new Vector3(0.08f, 0.28f, 0.08f), spec.leather, PrimitiveType.Cube);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var sn = sign > 0 ? snL : snR;
                Part(sn, "Wrap1", new Vector3(0.15f * sign, -0.62f, 0.075f), new Vector3(-10f, 0f, 4f * sign), new Vector3(0.09f, 0.035f, 0.09f), spec.wrap, PrimitiveType.Cube);
                Part(sn, "Wrap2", new Vector3(0.15f * sign, -0.71f, 0.065f), new Vector3(-10f, 0f, -4f * sign), new Vector3(0.088f, 0.035f, 0.088f), spec.wrap, PrimitiveType.Cube);
                Part(sn, "Wrap3", new Vector3(0.15f * sign, -0.79f, 0.055f), new Vector3(-10f, 0f, 3f * sign), new Vector3(0.086f, 0.035f, 0.086f), spec.wrap, PrimitiveType.Cube);
            }
            Part(ftL, "FootMesh", new Vector3(0.15f, -0.97f, 0.09f), new Vector3(6f, 0f, 0f), new Vector3(0.095f, 0.06f, 0.20f), spec.leather, PrimitiveType.Cube);
            Part(ftR, "FootMesh", new Vector3(-0.15f, -0.97f, 0.09f), new Vector3(6f, 0f, 0f), new Vector3(0.095f, 0.06f, 0.20f), spec.leather, PrimitiveType.Cube);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                var ft = sign > 0 ? ftL : ftR;
                Part(ft, "ToeClawIn", new Vector3(0.11f * sign, -0.99f, 0.21f), new Vector3(8f, -6f * sign, 0f), new Vector3(0.028f, 0.028f, 0.075f), spec.claw, PrimitiveType.Cube);
                Part(ft, "ToeClawOut", new Vector3(0.19f * sign, -0.99f, 0.20f), new Vector3(8f, 8f * sign, 0f), new Vector3(0.028f, 0.028f, 0.075f), spec.claw, PrimitiveType.Cube);
                Part(ft, "HeelClaw", new Vector3(0.15f * sign, -0.98f, -0.05f), new Vector3(-10f, 0f, 0f), new Vector3(0.024f, 0.024f, 0.06f), spec.claw, PrimitiveType.Cube);
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
                rb.mass = mass * s;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.None; // enabled while dynamic only

                Collider col;
                if (colType == 0)
                {
                    var bc = bone.gameObject.AddComponent<BoxCollider>();
                    bc.center = center * s; bc.size = size * s; col = bc;
                }
                else if (colType == 2)
                {
                    var sc = bone.gameObject.AddComponent<SphereCollider>();
                    sc.center = center * s; sc.radius = size.x * s; col = sc;
                }
                else
                {
                    var cap = bone.gameObject.AddComponent<CapsuleCollider>();
                    cap.center = center * s; cap.radius = size.x * s; cap.height = size.y * s; cap.direction = 1; col = cap;
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

            var brain = (ChaserEnemy)rootGO.AddComponent(spec.brain);
            brain.data = spec.data;
            if (brain is VandalEnemy vandal) vandal.muzzle = muzzleTip;

            if (spec.arcShield > 0f)
            {
                var shield = rootGO.AddComponent<EnergyShield>();
                shield.maxShield = spec.arcShield;
                shield.element = Element.Arc;
                shield.shellCenter = new Vector3(0f, 0f, 0.05f) * s;
                shield.shellScale = new Vector3(1.4f, 2.6f, 1.4f) * s;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, spec.path);
            Object.DestroyImmediate(rootGO);
            return prefab;
        }

        // ----- Dreg enemy prefab ------------------------------------------------

        private static GameObject BuildDregPrefab(Mats mats, EnemyData data)
        {
            return BuildEliksniPrefab(mats, new EliksniSpec
            {
                path = "Assets/Prefabs/DregEnemy.prefab",
                name = "DregEnemy",
                data = data,
                leather = mats.dregLeather, bone = mats.dregBone, cloth = mats.dregCloth,
                hair = mats.dregHair, claw = mats.dregClaw, wrap = mats.dregWrap, eye = mats.dregEye
            });
        }

        // ----- Shank drone rig (shared by Shank and Exploder Shank) --------------

        private class ShankSpec
        {
            public string path;
            public string name;
            public System.Type brain = typeof(ShankEnemy);
            public EnemyData data;
            public float hoverHeight = 2.5f;
            public Material glow;   // eye, thruster wash, gun tips
            public Material accent; // hatch and trim
            public bool guns = true;
            public bool countsAsKill = true; // off when the brain announces its own kills
        }

        /// <summary>
        /// Small hovering drone: sphere hull with a protruding front sensor
        /// eye (the crit zone), flat lift-fan cylinders port and starboard, a
        /// rear thruster, an antenna, and a pair of chin bolt guns. No
        /// CharacterController and no rig animator — ShankEnemy flies the
        /// transform directly.
        /// </summary>
        private static GameObject BuildShankPrefab(Mats mats, ShankSpec spec)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(spec.path);
            if (existing != null) return existing;

            var rootGO = new GameObject(spec.name) { layer = EnemyLayer };
            var root = rootGO.transform;

            var health = rootGO.AddComponent<Health>();
            health.SetMaxHealth(spec.data != null ? spec.data.maxHealth : 85f);

            GameObject Deco(string name, Vector3 pos, Vector3 euler, Vector3 scale,
                Material mat, PrimitiveType type, Transform parent = null)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent != null ? parent : root, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                return go;
            }

            // Hull keeps its collider: it is the hitscan target.
            var hull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hull.name = "Hull";
            hull.layer = EnemyLayer;
            hull.transform.SetParent(root, false);
            hull.transform.localScale = Vector3.one * 0.6f;
            hull.GetComponent<Renderer>().sharedMaterial = mats.shankBody;
            var hullHB = hull.AddComponent<Hitbox>();
            hullHB.owner = health;

            // Front sensor eye dome — sticks out past the hull, so head-on
            // shots that land on the lens count as precision hits.
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "Eye";
            eye.layer = EnemyLayer;
            eye.transform.SetParent(root, false);
            eye.transform.localPosition = new Vector3(0f, 0.01f, 0.26f);
            eye.transform.localScale = Vector3.one * 0.2f;
            eye.GetComponent<Renderer>().sharedMaterial = spec.glow;
            var eyeHB = eye.AddComponent<Hitbox>();
            eyeHB.owner = health;
            eyeHB.isCritZone = true;

            // Lift fans: flat cylinders port and starboard with glow wash beneath.
            for (int s = -1; s <= 1; s += 2)
            {
                Deco("Fan", new Vector3(0.34f * s, 0.08f, 0f), Vector3.zero, new Vector3(0.16f, 0.045f, 0.16f), mats.shankAccent, PrimitiveType.Cylinder);
                Deco("FanHub", new Vector3(0.34f * s, 0.13f, 0f), Vector3.zero, Vector3.one * 0.06f, mats.shankBody, PrimitiveType.Sphere);
                Deco("FanWash", new Vector3(0.34f * s, 0.02f, 0f), Vector3.zero, new Vector3(0.10f, 0.02f, 0.10f), spec.glow, PrimitiveType.Cylinder);
            }

            // Rear thruster with an exhaust glow.
            Deco("Thruster", new Vector3(0f, 0.04f, -0.32f), new Vector3(90f, 0f, 0f), new Vector3(0.10f, 0.07f, 0.10f), mats.shankAccent, PrimitiveType.Cylinder);
            Deco("ThrusterGlow", new Vector3(0f, 0.04f, -0.40f), Vector3.zero, Vector3.one * 0.06f, spec.glow, PrimitiveType.Sphere);

            // Top hatch plate and antenna.
            Deco("Hatch", new Vector3(0f, 0.27f, -0.02f), Vector3.zero, new Vector3(0.22f, 0.05f, 0.24f), spec.accent, PrimitiveType.Cube);
            Deco("Antenna", new Vector3(0.10f, 0.40f, -0.08f), new Vector3(0f, 0f, -8f), new Vector3(0.014f, 0.11f, 0.014f), mats.shankAccent, PrimitiveType.Cylinder);
            Deco("AntennaTip", new Vector3(0.115f, 0.52f, -0.08f), Vector3.zero, Vector3.one * 0.035f, spec.glow, PrimitiveType.Sphere);

            // Chin bolt guns (the Exploder trades them for more speed).
            if (spec.guns)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Deco("Gun", new Vector3(0.16f * s, -0.14f, 0.16f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.20f), mats.gunBlack, PrimitiveType.Cube);
                    Deco("GunTip", new Vector3(0.16f * s, -0.14f, 0.27f), Vector3.zero, Vector3.one * 0.04f, spec.glow, PrimitiveType.Sphere);
                }
            }

            rootGO.AddComponent<DamageFlash>();

            var brain = (ShankEnemy)rootGO.AddComponent(spec.brain);
            brain.data = spec.data;
            brain.hoverHeight = spec.hoverHeight;

            var rs = rootGO.AddComponent<Respawner>();
            rs.delay = 6f;
            rs.countsAsKill = spec.countsAsKill;

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, spec.path);
            Object.DestroyImmediate(rootGO);
            return prefab;
        }

        // ----- Vandal enemy prefab ------------------------------------------------

        /// <summary>Taller Eliksni with a long wire rifle and a Devils-red cloak.</summary>
        private static GameObject BuildVandalPrefab(Mats mats, EnemyData data)
        {
            return BuildEliksniPrefab(mats, new EliksniSpec
            {
                path = "Assets/Prefabs/VandalEnemy.prefab",
                name = "VandalEnemy",
                scale = 1.15f,
                weapon = EliksniWeapon.WireRifle,
                brain = typeof(VandalEnemy),
                data = data,
                leather = mats.dregLeather, bone = mats.dregBone, cloth = mats.vandalCloth,
                hair = mats.dregHair, claw = mats.dregClaw, wrap = mats.dregWrap, eye = mats.dregEye
            });
        }

        // ----- Shank enemy prefab -------------------------------------------------

        private static GameObject BuildShankPrefab(Mats mats, EnemyData data)
        {
            return BuildShankPrefab(mats, new ShankSpec
            {
                path = "Assets/Prefabs/ShankEnemy.prefab",
                name = "ShankEnemy",
                data = data,
                glow = mats.dregEye,
                accent = mats.shankAccent
            });
        }

        // ----- Exploder Shank enemy prefab ----------------------------------------

        /// <summary>Gunless Shank flying at face height, every glow swapped to
        /// warning red — readable as a bomb the instant it appears.</summary>
        private static GameObject BuildExploderShankPrefab(Mats mats, EnemyData data)
        {
            return BuildShankPrefab(mats, new ShankSpec
            {
                path = "Assets/Prefabs/ExploderShankEnemy.prefab",
                name = "ExploderShankEnemy",
                brain = typeof(ExploderShankEnemy),
                data = data,
                hoverHeight = 1.2f, // flies at the face, so the contact fuse can reach
                glow = mats.exploderEye,
                accent = mats.shankAccent,
                guns = false,
                countsAsKill = false // the brain announces shot-down kills itself
            });
        }

        // ----- Captain enemy prefab -----------------------------------------------

        /// <summary>Hulking 1.45x Eliksni in full bone regalia behind an arc
        /// shield. The Siriks boss reuses this spec at a bigger scale with
        /// corrupted materials.</summary>
        private static GameObject BuildCaptainPrefab(Mats mats, EnemyData data)
        {
            return BuildEliksniPrefab(mats, new EliksniSpec
            {
                path = "Assets/Prefabs/CaptainEnemy.prefab",
                name = "CaptainEnemy",
                scale = 1.45f,
                brain = typeof(CaptainEnemy),
                data = data,
                regalia = true,
                arcShield = 200f,
                leather = mats.dregLeather, bone = mats.dregBone, cloth = mats.captainCloth,
                hair = mats.dregHair, claw = mats.dregClaw, wrap = mats.dregWrap, eye = mats.dregEye
            });
        }
    }
}
