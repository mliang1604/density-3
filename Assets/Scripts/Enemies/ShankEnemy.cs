using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Enemies
{
    /// <summary>
    /// Hovering Fallen drone: sine-bob flight with tilt-into-movement (no
    /// CharacterController, no rig animator), orbits the player at mid range
    /// and peppers weak rapid bolts. Death is a small ether pop — despawn
    /// plus burst, no ragdoll; the Respawner handles the hide/revive cycle.
    /// The Exploder variant subclasses the movement and death hooks.
    /// </summary>
    public class ShankEnemy : MonoBehaviour
    {
        [Tooltip("Tuning asset. Left empty, runtime defaults matching the classic Shank apply.")]
        public EnemyData data;

        [Header("Hover")]
        public float hoverHeight = 2.5f;
        public float bobAmplitude = 0.18f;
        public float bobSpeed = 2.2f;
        [Tooltip("Degrees of lean per m/s of travel, capped at 18.")]
        public float tiltPerSpeed = 3f;

        protected Health health;
        protected Transform player;
        protected Health playerHealth;

        private float nextFire;
        private float orbitDir = 1f;
        private float nextOrbitFlip;
        private float bobPhase;
        private Vector3 smoothedVelocity;
        private Vector3 lastPos;

        // Ground/wall probes must not see the drone's own Enemy-layer collider.
        private static readonly int probeMask = Physics.DefaultRaycastLayers & ~(1 << 6);

        protected virtual void Awake()
        {
            health = GetComponent<Health>();
            if (data == null) data = DefaultData();
            if (health != null)
            {
                health.SetMaxHealth(data.maxHealth);
                health.Died += OnDied;
            }
            bobPhase = Random.value * Mathf.PI * 2f; // desync a cluster's bobbing
            lastPos = transform.position;
        }

        protected virtual void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        /// <summary>Canonical Shank tuning — shared by the bootstrap bake and
        /// the runtime fallback (ClassKits.Configure pattern).</summary>
        public static void Configure(EnemyData d)
        {
            d.displayName = "Shank";
            d.maxHealth = 85f;
            d.moveSpeed = 5.5f;
            d.strafeSpeed = 3.5f; // orbit speed
            d.aggroRange = 40f;
            d.preferredRange = 12f;
            d.fireRange = 25f;
            d.fireInterval = 0.5f;
            d.projectileDamage = 6f;
            d.projectileSpeed = 22f;
        }

        protected virtual EnemyData DefaultData()
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            Configure(d);
            return d;
        }

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                player = pc.transform;
                playerHealth = pc.GetComponent<Health>();
            }
        }

        private bool wasDead;

        private void Update()
        {
            if (health == null || health.IsDead)
            {
                wasDead |= health != null;
                return;
            }
            if (wasDead)
            {
                wasDead = false;
                OnRevived(); // the Respawner revives Health silently
            }
            if (player == null) return;

            float dt = Time.deltaTime;
            bobPhase += bobSpeed * dt;

            Vector3 toPlayer = player.position - transform.position;
            Vector3 flat = toPlayer; flat.y = 0f;
            float dist = flat.magnitude;
            bool engaged = dist <= data.aggroRange
                && playerHealth != null && !playerHealth.IsDead && dist > 0.01f;

            Vector3 move = engaged ? ComputeMove(flat / dist, dist) : Vector3.zero;
            ApplyFlight(move, engaged ? flat / Mathf.Max(dist, 0.01f) : transform.forward, dt);

            if (!engaged) return;
            Tick(dist);

            if (dist <= data.fireRange && Time.time >= nextFire
                && Vector3.Dot(transform.forward, flat / dist) > 0.85f)
            {
                nextFire = Time.time + data.fireInterval * Random.Range(0.85f, 1.25f);
                Fire();
            }
        }

        /// <summary>Per-frame hook for variant behavior (the Exploder's
        /// proximity fuse and beeper) — runs while engaged.</summary>
        protected virtual void Tick(float dist) { }

        /// <summary>Horizontal intent: close to the orbit band, then circle.
        /// The Exploder overrides nothing — its preferredRange of 0 turns
        /// this into a beeline.</summary>
        protected virtual Vector3 ComputeMove(Vector3 fwd, float dist)
        {
            if (Time.time >= nextOrbitFlip)
            {
                orbitDir = Random.value < 0.5f ? -1f : 1f;
                nextOrbitFlip = Time.time + Random.Range(2f, 4f);
            }
            if (dist > data.preferredRange + 2f) return fwd * data.moveSpeed;
            if (dist < data.preferredRange - 2f) return fwd * (-0.7f * data.moveSpeed);
            return Vector3.Cross(Vector3.up, fwd) * (data.strafeSpeed * orbitDir);
        }

        /// <summary>Moves the transform directly: horizontal step with a wall
        /// probe, altitude held at hoverHeight over the ground plus the bob,
        /// facing the player with a lean into the current velocity.</summary>
        private void ApplyFlight(Vector3 move, Vector3 face, float dt)
        {
            Vector3 pos = transform.position;

            Vector3 step = move * dt;
            if (step.sqrMagnitude > 0f)
            {
                // Blocked ahead: don't advance; circle the other way instead.
                if (Physics.Raycast(pos, step.normalized, step.magnitude + 0.5f, probeMask,
                        QueryTriggerInteraction.Ignore))
                {
                    orbitDir = -orbitDir;
                    step = Vector3.zero;
                }
            }
            pos += step;

            // Altitude: hover over whatever is below (ground, platforms).
            float targetY = pos.y;
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out RaycastHit ground, 15f,
                    probeMask, QueryTriggerInteraction.Ignore))
                targetY = ground.point.y + hoverHeight;
            targetY += Mathf.Sin(bobPhase) * bobAmplitude;
            pos.y = Mathf.Lerp(pos.y, targetY, 3f * dt);

            transform.position = pos;

            // Lean into motion: pitch forward when advancing, roll into strafes.
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, (pos - lastPos) / Mathf.Max(dt, 0.001f), 8f * dt);
            lastPos = pos;

            Quaternion look = Quaternion.LookRotation(face);
            float fwdSpeed = Vector3.Dot(smoothedVelocity, face);
            float latSpeed = Vector3.Dot(smoothedVelocity, Vector3.Cross(Vector3.up, face) * -1f);
            float pitch = Mathf.Clamp(fwdSpeed * tiltPerSpeed, -18f, 18f);
            float roll = Mathf.Clamp(latSpeed * tiltPerSpeed, -18f, 18f);
            Quaternion target = look * Quaternion.Euler(pitch, 0f, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 6f * dt);
        }

        /// <summary>Weak rapid bolt with a little scatter — pressure, not a sniper.</summary>
        protected virtual void Fire()
        {
            Vector3 origin = transform.position + transform.forward * 0.45f;
            Vector3 target = player.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.5f;
            Vector3 dir = (target - origin).normalized;

            // Walls only (the probe mask skips enemies); player is on Ignore Raycast.
            if (Physics.Linecast(origin, target, probeMask, QueryTriggerInteraction.Ignore)) return;

            var proj = FX.SpawnBolt(origin).AddComponent<EnemyProjectile>();
            proj.Launch(dir, playerHealth, data.projectileSpeed, data.projectileDamage);
            SFX.Play3D(SFX.BoltFireClip, origin, 0.5f, 1f, 1.35f);
        }

        /// <summary>Death pop: a puff of escaping ether, no ragdoll. The
        /// Respawner (same prefab) hides the body and revives it later.</summary>
        protected virtual void OnDied()
        {
            FX.SpawnElementBurst(transform.position, Element.Arc, 0.8f);
        }

        /// <summary>Runs on the first live frame after a respawn — variants
        /// reset one-shot state (the Exploder re-arms its fuse).</summary>
        protected virtual void OnRevived() { }
    }
}
