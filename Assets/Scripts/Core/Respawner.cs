using System.Collections;
using UnityEngine;

namespace FableFPS.Core
{
    /// <summary>
    /// Hides an entity when its Health dies, then revives it at its
    /// starting position after a delay. Used by dummies and enemies.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Respawner : MonoBehaviour
    {
        public float delay = 4f;
        public bool countsAsKill;

        /// <summary>Seconds the (still-visible) body lingers after death so a
        /// death animation can play before it's hidden. 0 = hide immediately.</summary>
        public float deathAnimSeconds;

        private Health health;
        private Vector3 startPos;
        private Quaternion startRot;

        private void Awake()
        {
            health = GetComponent<Health>();
            startPos = transform.position;
            startRot = transform.rotation;
            health.Died += OnDied;
        }

        private void OnDied()
        {
            if (countsAsKill)
            {
                GameEvents.RaiseEnemyKilled();
                SFX.Play3D(SFX.EnemyDeathClip, transform.position, 0.9f);
            }
            StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            if (deathAnimSeconds > 0f) yield return new WaitForSeconds(deathAnimSeconds);
            SetVisible(false);
            yield return new WaitForSeconds(Mathf.Max(0f, delay - deathAnimSeconds));
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.SetPositionAndRotation(startPos, startRot);
            if (cc != null) cc.enabled = true;
            health.Revive();
            SetVisible(true);
        }

        private void SetVisible(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }
    }
}
