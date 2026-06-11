using UnityEngine;
using FableFPS.Core;

namespace FableFPS.Weapons
{
    /// <summary>
    /// Destiny 2-style bullet magnetism (per BulletMagnetism_Spec.md): an invisible
    /// aim-assist cone projects from the barrel each frame; shots fired at a target
    /// inside the cone are bent toward it. Governed by Aim Assist (cone width),
    /// Range (cone depth), and Stability (bloom/recovery), modified by ADS.
    ///
    /// Direction only — damage, projectile physics, and audio live elsewhere.
    /// The spec's Fire()/bulletPrefab path is supported for projectile weapons;
    /// the hitscan HandCannon instead calls CalculateMagnetizedDirection +
    /// ApplyShotBloom directly.
    /// </summary>
    public class BulletMagnetism : MonoBehaviour
    {
        [Header("Weapon Stats (0-100)")]
        [Range(0, 100)] [SerializeField] private float aimAssist = 75f;
        [Range(0, 100)] [SerializeField] private float range = 60f;
        [Range(0, 100)] [SerializeField] private float stability = 55f;

        [Header("Bullet")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform barrelTip;

        [Header("ADS")]
        [SerializeField] private bool isADS = false;

        [Header("Detection")]
        [SerializeField] private LayerMask enemyLayers;

        private float _currentConeAngle;

        /// <summary>Cone half-angle in degrees: 1° at AA 0 → 12° at AA 100; ×0.6 in ADS.</summary>
        public float MaxConeAngle
        {
            get
            {
                float angle = Mathf.Lerp(1f, 12f, aimAssist / 100f);
                return isADS ? angle * 0.6f : angle;
            }
        }

        /// <summary>Cone depth in units: 10 at Range 0 → 40 at Range 100; ×1.4 in ADS.</summary>
        public float ConeDepth
        {
            get
            {
                float depth = Mathf.Lerp(10f, 40f, range / 100f);
                return isADS ? depth * 1.4f : depth;
            }
        }

        public float BloomPerShot => Mathf.Lerp(3f, 0.5f, stability / 100f);
        public float RecoverySpeed => Mathf.Lerp(5f, 25f, stability / 100f);

        private void Awake()
        {
            _currentConeAngle = MaxConeAngle;
        }

        private void Update()
        {
            _currentConeAngle = Mathf.MoveTowards(_currentConeAngle, MaxConeAngle,
                RecoverySpeed * Time.deltaTime);
        }

        /// <summary>Wires runtime references (used by GameBootstrap, which builds
        /// everything in code rather than via the Inspector).</summary>
        public void Configure(Transform tip, LayerMask layers)
        {
            barrelTip = tip;
            enemyLayers = layers;
            _currentConeAngle = MaxConeAngle;
        }

        /// <summary>Swaps in another weapon frame's stats (e.g. on loadout switch).</summary>
        public void SetStats(float newAimAssist, float newRange, float newStability)
        {
            aimAssist = Mathf.Clamp(newAimAssist, 0f, 100f);
            range = Mathf.Clamp(newRange, 0f, 100f);
            stability = Mathf.Clamp(newStability, 0f, 100f);
            _currentConeAngle = Mathf.Min(_currentConeAngle, MaxConeAngle);
        }

        public void SetADS(bool ads)
        {
            isADS = ads;
            _currentConeAngle = MaxConeAngle; // D2 resets bloom when entering ADS
        }

        /// <summary>Spec API for projectile weapons: magnetize, spawn, bloom.</summary>
        public void Fire()
        {
            if (barrelTip == null) return;
            Vector3 dir = CalculateMagnetizedDirection(barrelTip.position, barrelTip.forward);
            if (bulletPrefab != null)
                Instantiate(bulletPrefab, barrelTip.position, Quaternion.LookRotation(dir));
            ApplyShotBloom();
        }

        /// <summary>Per-shot bloom: the cone shrinks, floored at 20% of max.</summary>
        public void ApplyShotBloom()
        {
            _currentConeAngle = Mathf.Max(_currentConeAngle - BloomPerShot, MaxConeAngle * 0.2f);
        }

        /// <summary>
        /// Bends baseDir toward the best target inside the live cone. Bend strength
        /// eases from full at the cone center to none at the edge. Returns baseDir
        /// unchanged when nothing qualifies. The bent ray can still be blocked by
        /// cover — this is direction steering, not a hitbox expansion.
        /// </summary>
        public Vector3 CalculateMagnetizedDirection(Vector3 origin, Vector3 baseDir)
        {
            Vector3 result = baseDir;
            Transform target = FindBestTarget(origin, baseDir);
            if (target != null)
            {
                Vector3 toTarget = (target.position - origin).normalized;
                float angleToTarget = Vector3.Angle(baseDir, toTarget);
                if (angleToTarget <= _currentConeAngle)
                {
                    float t = 1f - (angleToTarget / _currentConeAngle); // 1 center, 0 edge
                    float bendStrength = Mathf.Pow(t, 0.5f);            // stronger near center
                    result = Vector3.Slerp(baseDir, toTarget, bendStrength);
                }
            }

            Debug.DrawRay(origin, result * ConeDepth, Color.green, 0.5f);
            Debug.DrawRay(origin, baseDir * ConeDepth, Color.yellow, 0.5f);
            return result;
        }

        private Transform FindBestTarget(Vector3 origin, Vector3 baseDir)
        {
            // Sphere cast approximates the cone: radius = cross-section at max depth.
            float radius = ConeDepth * Mathf.Tan(_currentConeAngle * Mathf.Deg2Rad);
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, baseDir, ConeDepth,
                enemyLayers, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestAngle = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;

                // Project extension to the spec: corpses are not targets.
                var h = hit.collider.GetComponentInParent<Health>();
                if (h != null && h.IsDead) continue;

                float angle = Vector3.Angle(baseDir, hit.transform.position - origin);
                if (angle <= _currentConeAngle && angle < bestAngle)
                {
                    bestAngle = angle;
                    best = hit.transform;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (barrelTip == null) return;
            if (Application.isPlaying)
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
                DrawWireCone(barrelTip.position, barrelTip.forward, _currentConeAngle, ConeDepth);
                if (_currentConeAngle < MaxConeAngle * 0.95f)
                {
                    Gizmos.color = new Color(1f, 1f, 0f, 0.07f);
                    DrawWireCone(barrelTip.position, barrelTip.forward, MaxConeAngle, ConeDepth);
                }
            }
            else
            {
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
                DrawWireCone(barrelTip.position, barrelTip.forward, MaxConeAngle, ConeDepth);
            }
        }

        private static void DrawWireCone(Vector3 origin, Vector3 forward, float halfAngleDeg, float length)
        {
            float radius = length * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
            Vector3 center = origin + forward * length;
            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f
                ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(forward, reference).normalized;
            Vector3 up = Vector3.Cross(right, forward).normalized;

            const int segments = 20;
            Vector3 prev = center + right * radius;
            for (int i = 1; i <= segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                Vector3 p = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
            for (int i = 0; i < 4; i++)
            {
                float a = (float)i / 4 * Mathf.PI * 2f;
                Vector3 rim = center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(origin, rim);
            }
        }
#endif
    }
}
