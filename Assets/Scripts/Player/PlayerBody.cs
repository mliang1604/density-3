using UnityEngine;

namespace Density3.Player
{
    /// <summary>
    /// Procedural first-person body: primitive torso/arms/legs under the
    /// camera so the guardian casts a shadow and looking down shows a body.
    /// No head — the camera lives there. Built at runtime in Awake (the
    /// committed prefab predates it; the bootstrap adds the component for
    /// fresh bakes). Squashes from the feet with the controller's crouch
    /// height so slides read right. Parts sit on Ignore Raycast with their
    /// colliders removed, like the rest of the player.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        private static Material armorMaterial;
        private static Material clothMaterial;

        private Transform bodyRoot;
        private CharacterController controller;
        private float standHeight;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            standHeight = controller != null ? controller.height : 1.8f;
            Build();
        }

        private void Build()
        {
            if (armorMaterial == null)
            {
                armorMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.30f, 0.32f, 0.38f) };
                clothMaterial = new Material(Shader.Find("Standard")) { color = new Color(0.16f, 0.17f, 0.22f) };
            }

            bodyRoot = new GameObject("Body").transform;
            bodyRoot.SetParent(transform, false);
            bodyRoot.localPosition = new Vector3(0f, -0.9f, 0f); // feet — crouch squash stays planted

            Part(PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.05f, 0f), new Vector3(0.42f, 0.32f, 0.28f), armorMaterial);
            Part(PrimitiveType.Cube, "Pelvis", new Vector3(0f, 0.72f, 0f), new Vector3(0.36f, 0.26f, 0.24f), clothMaterial);
            Part(PrimitiveType.Cube, "Leg.L", new Vector3(0.11f, 0.3f, 0f), new Vector3(0.15f, 0.6f, 0.17f), clothMaterial);
            Part(PrimitiveType.Cube, "Leg.R", new Vector3(-0.11f, 0.3f, 0f), new Vector3(0.15f, 0.6f, 0.17f), clothMaterial);
            Part(PrimitiveType.Sphere, "Shoulder.L", new Vector3(0.27f, 1.32f, 0f), Vector3.one * 0.2f, armorMaterial);
            Part(PrimitiveType.Sphere, "Shoulder.R", new Vector3(-0.27f, 1.32f, 0f), Vector3.one * 0.2f, armorMaterial);
            Part(PrimitiveType.Cube, "Arm.L", new Vector3(0.29f, 1.0f, 0f), new Vector3(0.12f, 0.5f, 0.14f), armorMaterial);
            Part(PrimitiveType.Cube, "Arm.R", new Vector3(-0.29f, 1.0f, 0f), new Vector3(0.12f, 0.5f, 0.14f), armorMaterial);
        }

        private void Part(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 2; // Ignore Raycast — invisible to hitscan and enemy LOS, like the capsule
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(bodyRoot, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void LateUpdate()
        {
            if (bodyRoot == null || controller == null) return;
            bodyRoot.localScale = new Vector3(1f, controller.height / standHeight, 1f);
        }
    }
}
