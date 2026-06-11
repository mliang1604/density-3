using System.Collections;
using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Owns a Dreg's death: switches the animated rig into a physics ragdoll,
    /// bursts the head into ether on a precision (headshot) kill, then hides the
    /// corpse and resets everything for respawn. Ragdoll references are serialized
    /// in the prefab; the bind pose is captured from the authored pose in Awake.
    /// </summary>
    public class DregDeath : MonoBehaviour
    {
        public float respawnDelay = 6f;
        public float corpseSeconds = 4f;
        public float knockback = 5f;

        [Header("Ragdoll (prefab references)")]
        public Rigidbody[] bodies;
        public Rigidbody pelvisBody;
        public Rigidbody chestBody;
        public Collider headCritCollider;
        public Renderer[] headRenderers;
        public Transform headBone;

        private Health health;
        private CharacterController cc;
        private DregAnimator animator;
        private Collider[] bodyColliders;
        private Renderer[] allRenderers;

        private Vector3[] bindLocalPos;
        private Quaternion[] bindLocalRot;
        private Vector3 startPos;
        private Quaternion startRot;
        private bool dead;

        private void Awake()
        {
            health = GetComponent<Health>();
            cc = GetComponent<CharacterController>();
            animator = GetComponent<DregAnimator>();

            if (bodies == null) bodies = new Rigidbody[0];
            bodyColliders = new Collider[bodies.Length];
            bindLocalPos = new Vector3[bodies.Length];
            bindLocalRot = new Quaternion[bodies.Length];
            for (int i = 0; i < bodies.Length; i++)
            {
                bodyColliders[i] = bodies[i].GetComponent<Collider>();
                bindLocalPos[i] = bodies[i].transform.localPosition;
                bindLocalRot[i] = bodies[i].transform.localRotation;
            }

            allRenderers = GetComponentsInChildren<Renderer>(true);
            startPos = transform.position;
            startRot = transform.rotation;

            if (health != null) health.Died += OnDied;
        }

        private void OnDestroy()
        {
            if (health != null) health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (dead) return;
            dead = true;

            GameEvents.AnnounceEnemyKilled(transform.position);

            bool precisionKill = health.LastDamage.isCrit;
            EnableRagdoll();
            if (precisionKill) ExplodeHead();

            StartCoroutine(Cycle());
        }

        private void EnableRagdoll()
        {
            if (animator != null) animator.enabled = false;
            if (cc != null) cc.enabled = false;

            // The crit zone is hit-detection only; the ragdoll head has its own
            // physics collider. Disabling stops corpse heads blocking live shots.
            if (headCritCollider != null) headCritCollider.enabled = false;

            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = false;
                bodies[i].useGravity = true;
                bodies[i].interpolation = RigidbodyInterpolation.Interpolate; // smooth only while dynamic
                if (bodyColliders[i] != null) bodyColliders[i].enabled = true;
            }

            // Knock the corpse away from whatever killed it.
            Vector3 src = health.LastDamage.source != null
                ? health.LastDamage.source.transform.position
                : transform.position - transform.forward;
            Vector3 dir = transform.position - src; dir.y = 0f;
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
            Vector3 impulse = (dir * 0.85f + Vector3.up * 0.5f).normalized * knockback;

            if (chestBody != null)
            {
                chestBody.AddForce(impulse, ForceMode.VelocityChange);
                chestBody.AddTorque(Random.insideUnitSphere * 4f, ForceMode.VelocityChange);
            }
            if (pelvisBody != null) pelvisBody.AddForce(impulse * 0.6f, ForceMode.VelocityChange);
        }

        private void ExplodeHead()
        {
            if (headRenderers != null)
                foreach (var r in headRenderers) if (r != null) r.enabled = false;

            Vector3 at = headBone != null ? headBone.position : transform.position + Vector3.up * 1.6f;
            FX.SpawnEtherBurst(at);
            // Large minDistance so the scream reads clearly over the gunshot
            // even at long-range kills.
            SFX.Play3D(SFX.EtherBurstClip, at, 1f, 10f);
        }

        private IEnumerator Cycle()
        {
            yield return new WaitForSeconds(corpseSeconds);
            SetRenderers(false);
            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay - corpseSeconds));
            ResetAndRespawn();
        }

        private void ResetAndRespawn()
        {
            // Physics off; bones back to their bind pose.
            for (int i = 0; i < bodies.Length; i++)
            {
                if (!bodies[i].isKinematic)
                {
                    bodies[i].linearVelocity = Vector3.zero;
                    bodies[i].angularVelocity = Vector3.zero;
                }
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
                bodies[i].interpolation = RigidbodyInterpolation.None;
                if (bodyColliders[i] != null) bodyColliders[i].enabled = false;
                bodies[i].transform.localPosition = bindLocalPos[i];
                bodies[i].transform.localRotation = bindLocalRot[i];
            }

            // Reattach the head.
            if (headRenderers != null)
                foreach (var r in headRenderers) if (r != null) r.enabled = true;
            if (headCritCollider != null) headCritCollider.enabled = true;

            CharacterTeleport.To(transform, startPos, startRot);

            if (animator != null)
            {
                animator.enabled = true;
                animator.OnRevived();
            }
            health.Revive();
            SetRenderers(true);
            dead = false;
        }

        private void SetRenderers(bool on)
        {
            if (allRenderers == null) return;
            foreach (var r in allRenderers) if (r != null) r.enabled = on;
        }
    }
}
