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
        }

        /// <summary>Bakes one EnemyData asset per archetype into Assets/Enemies —
        /// the roster's whole balance table, editable without touching prefabs.</summary>
        private static EnemyAssets BuildEnemyData()
        {
            EnsureFolder("Assets/Enemies");
            return new EnemyAssets
            {
                // EnemyData's class defaults ARE the classic Dreg tuning.
                dreg = CreateEnemyData("DregData", d => d.displayName = "Dreg")
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

        // ----- Dreg enemy prefab ------------------------------------------------

        /// <summary>
        /// Eliksni styled after the D2 reference render: white horned helmet with a
        /// maroon plume, four glowing eyes, heavy purple scarf and waist wrap, dark
        /// leathery body, FOUR full clawed arms, spiked bone pauldron, white
        /// bracers/knee guards, wrapped digitigrade shins, and clawed feet.
        /// </summary>
        private static GameObject BuildDregPrefab(Mats mats, EnemyData data)
        {
            const string path = "Assets/Prefabs/DregEnemy.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var rootGO = new GameObject("DregEnemy") { layer = EnemyLayer };
            var root = rootGO.transform;

            var health = rootGO.AddComponent<Health>();
            health.SetMaxHealth(data != null ? data.maxHealth : 150f);

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

            rootGO.AddComponent<ChaserEnemy>().data = data;

            var prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, path);
            Object.DestroyImmediate(rootGO);
            return prefab;
        }
    }
}
