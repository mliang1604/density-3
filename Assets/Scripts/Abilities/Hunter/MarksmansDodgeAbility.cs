using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.Weapons;

namespace Density3.Abilities
{
    /// <summary>
    /// Hunter class ability: a quick directional roll (the movement-input
    /// direction, forward when stationary) that reloads the equipped weapon
    /// mid-roll — including cleanly interrupting a reload in progress.
    /// Grounded only, like the rift.
    /// </summary>
    public class MarksmansDodgeAbility : AbilityBase
    {
        public float dodgeSpeed = 16f;
        public float dodgeSeconds = 0.3f; // ~5m of displacement

        private PlayerController player;
        private CharacterController cc;
        private HandCannon weapon;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
            weapon = GetComponent<HandCannon>();
        }

        /// <summary>Dodges are planted: no rolling mid-air.</summary>
        protected override bool CanActivate() => cc == null || cc.isGrounded;

        protected override void OnActivate()
        {
            Vector3 dir = transform.right * Input.GetAxisRaw("Horizontal")
                + transform.forward * Input.GetAxisRaw("Vertical");
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;

            if (player != null)
            {
                player.OverrideMove(dir.normalized * dodgeSpeed, dodgeSeconds);
                player.AddRecoil(-3f, 0f); // quick camera dip that self-recovers
            }
            if (weapon != null) weapon.RefillMag(); // also cancels an in-progress reload

            SFX.Play2D(SFX.AbilityThrowClip, 0.4f, 0.85f);
            FX.SpawnElementBurst(transform.position + Vector3.down * 0.5f, Element.Solar, 0.5f);
        }
    }
}
