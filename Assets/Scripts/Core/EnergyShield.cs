using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Regenerating energy pool layered in front of a Health as a Damaged
    /// interceptor — Health semantics stay untouched. Every ApplyDamage path
    /// (hitscan, projectiles, ability AoE) lands on Health first; this
    /// component, inside the Damaged event, refunds whatever the shield can
    /// absorb via Heal. Health raises Damaged before its death check, so a
    /// shielded hit can never kill through the shield.
    ///
    /// Crit gating lives in Hitbox (precision hits don't count while IsUp);
    /// the visible shell is built procedurally at runtime, so committed
    /// prefabs need no rebake to show it.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class EnergyShield : MonoBehaviour
    {
        public float maxShield = 200f;
        [Tooltip("Seconds without taking ANY damage before the pool starts refilling.")]
        public float regenDelay = 5f;
        public float regenRate = 80f;
        public Element element = Element.Arc;

        [Header("Shell (built at runtime)")]
        public Vector3 shellCenter = Vector3.zero;
        public Vector3 shellScale = new Vector3(1.3f, 2.4f, 1.3f);

        public float Current { get; private set; }
        public bool IsUp => Current > 0f && health != null && !health.IsDead;

        private Health health;
        private float lastHitTime = -999f;
        private float hitFlash;
        private bool wasDead;
        private bool announcedBreak;

        private Renderer shellRenderer;
        private Material shellMaterial;

        private void Awake()
        {
            health = GetComponent<Health>();
            health.Damaged += OnDamaged;
            Current = maxShield;
            BuildShell();
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        /// <summary>The interceptor: runs inside Health.Damaged, before the
        /// death check. Absorbs into the pool and refunds the Health.</summary>
        private void OnDamaged(DamageInfo info)
        {
            lastHitTime = Time.time; // any damage resets regen, shielded or not
            if (health.IsDead || Current <= 0f) return;

            float absorbed = Mathf.Min(Current, info.amount);
            Current -= absorbed;
            health.Heal(absorbed);

            hitFlash = 1f;
            SFX.Play3D(SFX.ArcZapClip, info.hitPoint, 0.25f, 4f, 1.6f);

            if (Current <= 0f && !announcedBreak)
            {
                announcedBreak = true;
                Vector3 center = transform.TransformPoint(shellCenter);
                FX.SpawnElementBurst(center, element, 1.5f);
                SFX.Play3D(SFX.ArcShockClip, center, 1f, 12f);
            }
        }

        private void Update()
        {
            if (health.IsDead)
            {
                wasDead = true;
                if (shellRenderer != null) shellRenderer.enabled = false;
                return;
            }
            if (wasDead) // revived by the death cycle — come back fully shielded
            {
                wasDead = false;
                Current = maxShield;
                announcedBreak = false;
            }

            if (Current < maxShield && Time.time - lastHitTime >= regenDelay)
            {
                Current = Mathf.Min(maxShield, Current + regenRate * Time.deltaTime);
                if (Current > 0f) announcedBreak = false;
            }

            hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime / 0.18f);
            UpdateShell();
        }

        /// <summary>Translucent ellipsoid shell — visible exactly while the
        /// pool holds anything (visual edge = mechanical truth), brightening
        /// with fill level and flaring where hits land.</summary>
        private void BuildShell()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ShieldShell";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = shellCenter;
            go.transform.localScale = shellScale;

            shellMaterial = new Material(Shader.Find("Sprites/Default"));
            shellRenderer = go.GetComponent<Renderer>();
            shellRenderer.material = shellMaterial;
            shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;
            UpdateShell();
        }

        private void UpdateShell()
        {
            if (shellRenderer == null) return;
            bool up = Current > 0f;
            shellRenderer.enabled = up;
            if (!up) return;

            float fill = Current / maxShield;
            Color c = ElementPalette.Base(element);
            c.a = 0.10f + 0.10f * fill + 0.35f * hitFlash;
            shellMaterial.color = c;
        }
    }
}
