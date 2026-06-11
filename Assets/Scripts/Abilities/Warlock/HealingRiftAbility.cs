using System.Collections;
using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Warlock class ability: a short planted cast, then a healing rift at
    /// the caster's feet. The cast briefly locks movement (and with it the
    /// other ability binds, which gate on MovementLocked); if the caster
    /// dies mid-cast the unlock is left to the respawn flow so the two
    /// never fight over the flag.
    /// </summary>
    public class HealingRiftAbility : AbilityBase
    {
        public float castSeconds = 1f;
        public float riftSeconds = 15f;
        public float riftRadius = 2.2f;
        public float healPerSecond = 20f;

        private PlayerController player;
        private Health health;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
            health = GetComponent<Health>();
        }

        protected override void OnActivate() => StartCoroutine(Cast());

        private IEnumerator Cast()
        {
            if (player != null) player.MovementLocked = true;
            SFX.Play2D(SFX.AbilityThrowClip, 0.5f, 0.7f);
            yield return new WaitForSeconds(castSeconds);

            if (health != null && health.IsDead) yield break; // respawn owns the lock now
            if (player != null) player.MovementLocked = false;

            // Plant the rift on the ground beneath the caster.
            Vector3 at = transform.position;
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                at = hit.point + Vector3.up * 0.05f;

            var zone = new GameObject("HealingRift").AddComponent<RiftZone>();
            zone.transform.position = at;
            zone.Configure(health, riftRadius, riftSeconds, healPerSecond);
            FX.SpawnElementBurst(at, Element.Void, 0.9f);
            SFX.Play2D(SFX.AbilityReadyClip, 0.45f, 0.65f);
        }
    }
}
