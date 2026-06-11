using UnityEngine;
using Density3.Core;

namespace Density3.Abilities
{
    /// <summary>
    /// The glowing ring a Healing Rift plants on the ground: heals its owner
    /// continuously while they stand inside the radius (horizontal check, so
    /// jumping doesn't drop the heal). Visuals are a procedural LineRenderer
    /// circle — the world-space cousin of the HUD's procedural ring sprite —
    /// plus a soft void light, both pulsing gently. Expires after its
    /// duration with a small burst.
    /// </summary>
    public class RiftZone : MonoBehaviour
    {
        private Health target;
        private float radius;
        private float remaining;
        private float healPerSecond;
        private LineRenderer ring;
        private Light glow;
        private float age;

        public void Configure(Health owner, float zoneRadius, float seconds, float hps)
        {
            target = owner;
            radius = zoneRadius;
            remaining = seconds;
            healPerSecond = hps;

            Color c = ElementPalette.Base(Element.Void);

            ring = FX.SpawnGroundRing(transform.position, radius, Element.Void, 0.09f);
            ring.transform.SetParent(transform, false); // expires with the rift

            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = c;
            glow.range = radius * 2.2f;
            glow.intensity = 1.6f;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            age += Time.deltaTime;

            // Gentle pulse so the rift reads as live.
            float pulse = 0.75f + 0.25f * Mathf.Sin(age * 4f);
            if (glow != null) glow.intensity = 1.6f * pulse;
            if (ring != null)
            {
                Color c = ring.startColor;
                c.a = 0.55f + 0.35f * pulse;
                ring.startColor = c;
                ring.endColor = c;
            }

            if (target != null && !target.IsDead)
            {
                Vector3 d = target.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude <= radius * radius)
                    target.Heal(healPerSecond * Time.deltaTime);
            }

            if (remaining <= 0f)
            {
                FX.SpawnElementBurst(transform.position, Element.Void, 0.5f);
                Destroy(gameObject);
            }
        }
    }
}
