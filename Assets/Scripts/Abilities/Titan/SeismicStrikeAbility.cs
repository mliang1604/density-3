using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Abilities
{
    /// <summary>
    /// Titan melee: a sprint-activated shoulder charge. Only chargeable
    /// while sprinting — a non-sprint melee press fails the gate (costing
    /// nothing) and falls through to PlayerAbilities' basic punch. The lunge
    /// drives ~6m along the aim via OverrideMove; on contact it stops, hits
    /// the target flat (no crits, lethal to a Dreg), shocks a small Arc AoE
    /// around it, and shoves nearby enemies back through their own
    /// CharacterControllers. No self-damage: the caster is AoE-exempt.
    /// </summary>
    public class SeismicStrikeAbility : AbilityBase
    {
        public float lungeSpeed = 20f;
        public float lungeSeconds = 0.3f; // ~6m
        public float contactDamage = 160f;
        public float aoeDamage = 80f;
        public float aoeRadius = 3f;
        public float shoveDistance = 1.6f;

        private static readonly Collider[] overlaps = new Collider[32];
        private static readonly HashSet<CharacterController> shoved = new HashSet<CharacterController>();

        private PlayerController player;
        private float lungeEnd;
        private bool lunging;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
        }

        /// <summary>The charge needs a running start; otherwise the press
        /// falls through to the basic punch without spending the bar.</summary>
        protected override bool CanActivate() => player != null && player.IsSprinting;

        protected override void OnActivate()
        {
            Transform cam = player.playerCamera != null ? player.playerCamera.transform : transform;
            Vector3 dir = cam.forward;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;

            player.OverrideMove(dir * lungeSpeed, lungeSeconds);
            lunging = true;
            lungeEnd = Time.time + lungeSeconds;
            SFX.Play2D(SFX.AbilityMeleeClip, 0.8f, 0.7f);
        }

        protected override void Update()
        {
            base.Update();
            if (!lunging) return;
            if (Time.time >= lungeEnd)
            {
                lunging = false;
                return;
            }

            // Contact check just ahead of the chest while charging.
            Vector3 probe = transform.position + transform.forward * 0.9f;
            int n = Physics.OverlapSphereNonAlloc(probe, 0.9f, overlaps, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var hb = overlaps[i].GetComponentInParent<Hitbox>();
                if (hb == null || hb.owner == null || hb.owner.IsDead) continue;
                if (hb.owner.gameObject == gameObject) continue;
                Impact(hb, overlaps[i].ClosestPoint(probe));
                return;
            }
        }

        private void Impact(Hitbox hb, Vector3 at)
        {
            lunging = false;
            player.OverrideMove(Vector3.zero, 0f); // the wall of muscle stops here

            float applied = hb.Hit(contactDamage, 1f, at, gameObject); // no crits
            if (applied > 0f) DamageNumbers.Spawn(at, applied, false);

            AoEDamage.Apply(at, aoeRadius, aoeDamage, gameObject);
            Shove(at);
            FX.SpawnElementBurst(at, Element.Arc, 1.2f);
            SFX.Play3D(SFX.MeleeImpactClip, at, 0.95f, 7f);
            SFX.Play3D(SFX.AbilityDetonateClip, at, 0.6f, 7f);
        }

        /// <summary>One-shot outward shove through each victim's own
        /// CharacterController, so walls still stop them.</summary>
        private void Shove(Vector3 center)
        {
            int n = Physics.OverlapSphereNonAlloc(center, aoeRadius, overlaps, ~0,
                QueryTriggerInteraction.Ignore);
            shoved.Clear();
            for (int i = 0; i < n; i++)
            {
                var cc = overlaps[i].GetComponentInParent<CharacterController>();
                if (cc == null || !cc.enabled || !shoved.Add(cc)) continue;
                if (cc.gameObject == gameObject) continue;
                Vector3 away = cc.transform.position - center;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = transform.forward;
                cc.Move(away.normalized * shoveDistance);
            }
        }
    }
}
