using UnityEngine;
using FableFPS.Core;
using FableFPS.Player;

namespace FableFPS.Enemies
{
    /// <summary>
    /// Simple Fallen-style combatant: closes to a preferred range,
    /// strafes, and fires slow energy bolts at the player.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ChaserEnemy : MonoBehaviour
    {
        public float moveSpeed = 4.5f;
        public float strafeSpeed = 3f;
        public float aggroRange = 45f;
        public float preferredRange = 14f;
        public float fireRange = 30f;
        public float fireInterval = 2.2f;
        public float projectileDamage = 14f;
        public float projectileSpeed = 17f;

        /// <summary>Raised each time a bolt is fired (drives the animator's recoil).</summary>
        public event System.Action Fired;

        private CharacterController cc;
        private Health health;
        private Transform player;
        private Health playerHealth;
        private float nextFire;
        private float strafeDir = 1f;
        private float nextStrafeFlip;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<Health>();
        }

        private void Start()
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

            if (dist > aggroRange)
            {
                cc.SimpleMove(Vector3.zero);
                return;
            }

            Vector3 fwd = toPlayer.normalized;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(fwd), 8f * Time.deltaTime);

            if (Time.time >= nextStrafeFlip)
            {
                strafeDir = Random.value < 0.5f ? -1f : 1f;
                nextStrafeFlip = Time.time + Random.Range(1f, 2.5f);
            }

            Vector3 move = dist > preferredRange
                ? fwd * moveSpeed
                : Vector3.Cross(Vector3.up, fwd) * (strafeSpeed * strafeDir);
            cc.SimpleMove(move);

            if (dist <= fireRange && Time.time >= nextFire)
            {
                nextFire = Time.time + fireInterval * Random.Range(0.8f, 1.3f);
                Fire();
            }
        }

        private void Fire()
        {
            Vector3 origin = transform.position + Vector3.up * 0.6f + transform.forward * 0.9f;
            Vector3 target = player.position + Vector3.up * 0.5f;
            Vector3 dir = (target - origin).normalized;

            // Player is on the Ignore Raycast layer, so a hit here means a wall is in the way.
            if (Physics.Linecast(origin, target)) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "EnemyBolt";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.22f;
            go.GetComponent<Renderer>().material = ProjectileMaterial();
            var proj = go.AddComponent<EnemyProjectile>();
            proj.Launch(dir, playerHealth, projectileSpeed, projectileDamage);
            SFX.Play3D(SFX.BoltFireClip, origin, 0.8f);
            Fired?.Invoke();
        }

        private static Material projMat;

        private static Material ProjectileMaterial()
        {
            if (projMat == null)
            {
                projMat = new Material(Shader.Find("Standard"));
                projMat.color = new Color(0.6f, 0.2f, 1f);
                projMat.EnableKeyword("_EMISSION");
                projMat.SetColor("_EmissionColor", new Color(0.55f, 0.2f, 1f) * 2.5f);
            }
            return projMat;
        }
    }
}
