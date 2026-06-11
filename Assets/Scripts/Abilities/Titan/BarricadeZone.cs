using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The planted barricade: three translucent arc panels (center plus two
    /// angled wings) with glowing top edges, real default-layer colliders —
    /// hitscan, enemy bolts, and the enemy line-of-sight check all treat it
    /// as a wall — and one shared Health pool routed via Hitbox so sustained
    /// fire breaks it. Collapses with a burst at death or after its
    /// lifetime; DamageFlash gives hit feedback.
    /// </summary>
    public class BarricadeZone : MonoBehaviour
    {
        private static Material panelMaterial;
        private static Material edgeMaterial;

        private Health health;
        private float remaining;
        private bool collapsed;

        public void Configure(float hp, float lifetimeSeconds)
        {
            remaining = lifetimeSeconds;

            if (panelMaterial == null)
            {
                // Translucent unlit arc panel (pinned shader), glowing top edge.
                panelMaterial = new Material(Shader.Find("Sprites/Default"));
                Color body = ElementPalette.Base(Element.Arc) * 0.5f;
                body.a = 0.55f;
                panelMaterial.color = body;
                edgeMaterial = new Material(Shader.Find("Standard"));
                edgeMaterial.color = ElementPalette.Base(Element.Arc);
                edgeMaterial.EnableKeyword("_EMISSION");
                edgeMaterial.SetColor("_EmissionColor", ElementPalette.Emission(Element.Arc, 1.8f));
            }

            health = gameObject.AddComponent<Health>();
            health.SetMaxHealth(hp); // no regen: barricades break and stay broken
            health.Died += Collapse;

            Panel("Center", new Vector3(0f, 0.9f, 0f), 0f);
            Panel("Wing.L", new Vector3(-1.05f, 0.9f, 0.18f), 24f);
            Panel("Wing.R", new Vector3(1.05f, 0.9f, 0.18f), -24f);

            gameObject.AddComponent<DamageFlash>();

            var glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = ElementPalette.Base(Element.Arc);
            glow.range = 3.5f;
            glow.intensity = 1.8f;
            glow.transform.localPosition = Vector3.up * 1.5f;
        }

        private void Panel(string name, Vector3 localPos, float yaw)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its collider: it IS a wall
            panel.name = name;
            panel.transform.SetParent(transform, false);
            panel.transform.localPosition = localPos;
            panel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            panel.transform.localScale = new Vector3(1.15f, 1.8f, 0.16f);
            panel.GetComponent<Renderer>().sharedMaterial = panelMaterial;
            var hb = panel.AddComponent<Hitbox>();
            hb.owner = health;

            var edge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            edge.name = name + "Edge";
            Destroy(edge.GetComponent<Collider>());
            edge.transform.SetParent(panel.transform, false);
            edge.transform.localPosition = new Vector3(0f, 0.52f, 0f);
            edge.transform.localScale = new Vector3(1.02f, 0.04f, 1.2f);
            edge.GetComponent<Renderer>().sharedMaterial = edgeMaterial;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Collapse();
        }

        private void Collapse()
        {
            if (collapsed) return;
            collapsed = true;
            FX.SpawnElementBurst(transform.position + Vector3.up * 0.9f, Element.Arc, 1.3f);
            SFX.Play3D(SFX.AbilityDetonateClip, transform.position, 0.7f, 7f);
            Destroy(gameObject);
        }
    }
}
