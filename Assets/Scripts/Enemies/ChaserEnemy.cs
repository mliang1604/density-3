using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Enemies
{
    /// <summary>
    /// Data-driven Fallen combatant: closes to a preferred range, strafes,
    /// and fires energy bolts at the player. Tuning lives in an EnemyData
    /// asset; variants override the fire and preferred-range hooks rather
    /// than forking the brain.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ChaserEnemy : MonoBehaviour
    {
        [Tooltip("Tuning asset. Left empty, runtime defaults matching the classic Dreg apply.")]
        public EnemyData data;

        /// <summary>Raised each time a bolt is fired (drives the animator's recoil).</summary>
        public event System.Action Fired;

        protected CharacterController cc;
        protected Health health;
        protected Transform player;
        protected Health playerHealth;

        private float nextFire;
        private float strafeDir = 1f;
        private float nextStrafeFlip;

        protected virtual void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<Health>();

            if (data == null) data = DefaultData();
            if (health != null) health.SetMaxHealth(data.maxHealth);
        }

        /// <summary>Runtime fallback for prefabs committed without a data asset.
        /// EnemyData's class defaults ARE the Dreg; variants override.</summary>
        protected virtual EnemyData DefaultData() => ScriptableObject.CreateInstance<EnemyData>();

        protected virtual void Start()
        {
            // Cross-prefab reference: resolve the player at runtime.
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                player = pc.transform;
                playerHealth = pc.GetComponent<Health>();
            }
        }

        private void Update()
        {
            if (health == null || health.IsDead || player == null) return;
            if (playerHealth != null && playerHealth.IsDead) return;

            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < 0.01f) return;

            if (dist > data.aggroRange)
            {
                cc.SimpleMove(Vector3.zero);
                return;
            }

            Vector3 fwd = toPlayer.normalized;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(fwd), 8f * Time.deltaTime);

            cc.SimpleMove(ComputeMove(fwd, dist));

            Tick(fwd, dist);

            if (Time.time >= nextFire && ReadyToFire(dist))
            {
                nextFire = Time.time + data.fireInterval * Random.Range(0.8f, 1.3f);
                Fire();
            }
        }

        /// <summary>Preferred-range behavior: advance until inside preferred
        /// range, then strafe. Variants override to hold range or rush.</summary>
        protected virtual Vector3 ComputeMove(Vector3 fwd, float dist)
        {
            if (Time.time >= nextStrafeFlip)
            {
                strafeDir = Random.value < 0.5f ? -1f : 1f;
                nextStrafeFlip = Time.time + Random.Range(1f, 2.5f);
            }
            return dist > data.preferredRange
                ? fwd * data.moveSpeed
                : Vector3.Cross(Vector3.up, fwd) * (data.strafeSpeed * strafeDir);
        }

        /// <summary>Per-frame hook for variant-specific behavior (melee swipes,
        /// telegraph upkeep) — runs while aggroed, after movement.</summary>
        protected virtual void Tick(Vector3 fwd, float dist) { }

        /// <summary>Range gate for the ranged attack; the interval gate is the caller's.</summary>
        protected virtual bool ReadyToFire(float dist) => dist <= data.fireRange;

        /// <summary>Fire behavior: a single bolt at the player's chest.
        /// Variants override for telegraphs and volleys.</summary>
        protected virtual void Fire()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f + transform.forward * 0.9f;
            Vector3 target = player.position + Vector3.up * 0.5f;
            Vector3 dir = (target - origin).normalized;

            // Player is on the Ignore Raycast layer, so a hit here means a wall is in the way.
            if (Physics.Linecast(origin, target)) return;

            var proj = FX.SpawnBolt(origin).AddComponent<EnemyProjectile>();
            proj.Launch(dir, playerHealth, data.projectileSpeed, data.projectileDamage);
            SFX.Play3D(SFX.BoltFireClip, origin, 0.8f);
            RaiseFired();
        }

        /// <summary>Lets variant fire hooks drive the animator's recoil.</summary>
        protected void RaiseFired() => Fired?.Invoke();
    }
}
