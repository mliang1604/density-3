using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Routes ability input to the four ability slots. Binds follow the
    /// project's serialized-KeyCode convention; G/V/F/Q are free keys that
    /// browsers don't claim in WebGL. Slots are populated by ClassLoadout at
    /// spawn — empty slots no-op, so the router is safe on any player.
    /// </summary>
    public class PlayerAbilities : MonoBehaviour
    {
        [Header("Binds")]
        public KeyCode grenadeKey = KeyCode.G;
        public KeyCode meleeKey = KeyCode.V;
        public KeyCode classAbilityKey = KeyCode.F;
        public KeyCode superKey = KeyCode.Q;

        [Header("Slots (populated by ClassLoadout)")]
        public AbilityBase grenade;
        public AbilityBase melee;
        public AbilityBase classAbility;
        public AbilityBase super;

        private PlayerController controller;
        private Health health;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (health != null && health.IsDead) return;
            if (controller != null && controller.MovementLocked) return;

            if (Input.GetKeyDown(grenadeKey)) Activate(grenade);
            if (Input.GetKeyDown(meleeKey)) Activate(melee);
            if (Input.GetKeyDown(classAbilityKey)) Activate(classAbility);
            if (Input.GetKeyDown(superKey)) Activate(super);
        }

        private static void Activate(AbilityBase ability)
        {
            if (ability != null) ability.TryActivate();
        }
    }
}
