using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Total damage nullification for boss gates, toggled by gate logic.
    /// This component is the flag and the visuals: Hitbox checks Immune and
    /// skips damage entirely (the IMMUNE number path), and a pulsing pale
    /// shell makes the state readable at a glance — lit means bullets do
    /// nothing. Clamping against direct-damage paths lives in BossGate,
    /// which knows the gate threshold; Health itself is untouched.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class ImmunityShield : MonoBehaviour
    {
        public Color shellColor = new Color(1f, 0.95f, 0.75f);
        public Vector3 shellCenter = Vector3.zero;
        public Vector3 shellScale = new Vector3(1.3f, 2.4f, 1.3f);

        public bool Immune { get; private set; }

        private Health health;
        private Renderer shellRenderer;
        private Material shellMaterial;
        private float pulse;

        private void Awake()
        {
            health = GetComponent<Health>();
            BuildShell();
        }

        /// <summary>Raise or drop immunity with a burst and a sting — the
        /// state change must be unmissable from across the room.</summary>
        public void SetImmune(bool value)
        {
            if (Immune == value) return;
            Immune = value;
            if (shellRenderer != null) shellRenderer.enabled = value;

            Vector3 at = transform.TransformPoint(shellCenter);
            FX.SpawnColorBurst(at, shellColor, value ? 1.7f : 1.2f);
            SFX.Play3D(value ? SFX.SuperActivateClip : SFX.ArcShockClip, at, 0.8f, 14f,
                value ? 0.6f : 1.1f);
        }

        private void Update()
        {
            if (shellRenderer == null || !shellRenderer.enabled) return;
            if (health.IsDead)
            {
                shellRenderer.enabled = false;
                return;
            }
            pulse += Time.deltaTime * 3f;
            Color c = shellColor;
            c.a = 0.16f + 0.07f * Mathf.Sin(pulse);
            shellMaterial.color = c;
        }

        /// <summary>Translucent pale ellipsoid, built at runtime (committed
        /// prefabs need no rebake), hidden until immunity raises it.</summary>
        private void BuildShell()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImmunityShell";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = shellCenter;
            go.transform.localScale = shellScale;

            shellMaterial = new Material(Shader.Find("Sprites/Default"));
            shellRenderer = go.GetComponent<Renderer>();
            shellRenderer.material = shellMaterial;
            shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;
            shellRenderer.enabled = false;
        }
    }
}
