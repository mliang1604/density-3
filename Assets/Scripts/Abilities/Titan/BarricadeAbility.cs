using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Abilities
{
    /// <summary>
    /// Titan class ability: plants a Towering Barricade two meters ahead —
    /// a destructible arc-energy wall with a real collider, so it bodily
    /// blocks movement and enemy bolts, and enemy AI holds fire behind it
    /// for free (ChaserEnemy linecasts before shooting). Grounded casts only.
    /// </summary>
    public class BarricadeAbility : AbilityBase
    {
        public float wallHealth = 400f;
        public float lifetimeSeconds = 25f;
        public float placeDistance = 2.2f;

        private PlayerController player;
        private CharacterController cc;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
        }

        /// <summary>Barricades are planted: no casting mid-air.</summary>
        protected override bool CanActivate() => cc == null || cc.isGrounded;

        protected override void OnActivate()
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;

            Vector3 at = transform.position + fwd * placeDistance;
            if (Physics.Raycast(at + Vector3.up, Vector3.down, out RaycastHit ground, 4f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                at = ground.point;
            else
                at += Vector3.down * 0.9f; // best effort: player feet height

            var zone = new GameObject("TowerBarricade").AddComponent<BarricadeZone>();
            zone.transform.SetPositionAndRotation(at, Quaternion.LookRotation(fwd));
            zone.Configure(wallHealth, lifetimeSeconds);

            FX.SpawnElementBurst(at + Vector3.up * 0.5f, Element.Arc, 0.9f);
            SFX.Play3D(SFX.MeleeImpactClip, at, 0.8f, 6f);
        }
    }
}
