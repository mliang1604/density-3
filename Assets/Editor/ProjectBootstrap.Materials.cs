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
    }
}
