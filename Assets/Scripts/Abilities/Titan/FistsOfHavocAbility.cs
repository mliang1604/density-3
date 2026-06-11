using UnityEngine;
using Density3.Core;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.Abilities
{
    /// <summary>
    /// Titan super: a 15s roaming mode. The weapon holsters (arc crackles in
    /// the empty hands), movement speeds up, and LMB becomes the slam — a
    /// forward leap from the ground, or a hard dive when airborne, with a
    /// 250-damage arc shock on landing. Energy is the full bar; the timer
    /// runs regardless of how many slams land.
    /// </summary>
    public class FistsOfHavocAbility : AbilityBase
    {
        public float durationSeconds = 15f;
        public float speedBuff = 1.25f;
        public float slamDamage = 250f;
        public float slamRadius = 5f;
        public float chainDamage = 60f;
        public float aftershockDamagePerTick = 15f;
        public float aftershockSeconds = 4f;
        public float leapForward = 8f;
        public float leapUp = 6f;
        public float diveDown = 18f;

        private PlayerController player;
        private HandCannon weapon;
        private float endTime;
        private bool active;
        private bool slamPending;
        private float slamArmedAt;
        private ParticleSystem fistArcs;

        protected override void Awake()
        {
            base.Awake();
            player = GetComponent<PlayerController>();
            weapon = GetComponent<HandCannon>();
        }

        protected override void OnActivate()
        {
            active = true;
            endTime = Time.time + durationSeconds;
            suppressKillEnergy = true; // slam kills must not refund the super

            if (weapon != null)
            {
                weapon.SetHolstered(true);
                if (weapon.viewmodel != null)
                {
                    // Arc crackle where the hands are for the whole super —
                    // sparks and a sparking loop, both dying with the embers.
                    fistArcs = FX.AttachEmbers(weapon.viewmodel.gameObject, Element.Arc, 20f, 0.028f);
                    fistArcs.transform.localPosition = new Vector3(0f, -0.12f, 0.35f);
                    var crackle = SFX.AttachLoop(fistArcs.gameObject, SFX.ArcLoopClip, 0.35f, 3f);
                    if (crackle != null) crackle.spatialBlend = 0f; // the player's own hands
                }
            }
            if (player != null) player.SpeedScale = speedBuff; // holstered: ADS won't rewrite it

            SFX.Play2D(SFX.SuperActivateClip, 0.95f, 0.85f);
            var hud = FindFirstObjectByType<HUDController>();
            if (hud != null) hud.PulseVignette(ElementPalette.Base(Element.Arc), 0.45f);
        }

        protected override void Update()
        {
            base.Update();
            if (!active) return;

            if (Input.GetMouseButtonDown(0) && !slamPending && player != null)
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;

                // Grounded: leap up and forward, slam where you land.
                // Airborne: dive straight down into the slam.
                player.AddImpulse(player.IsGrounded
                    ? fwd * leapForward + Vector3.up * leapUp
                    : fwd * 3f + Vector3.down * diveDown);
                slamPending = true;
                slamArmedAt = Time.time + 0.15f; // clear the launch frames
                SFX.Play2D(SFX.AbilityMeleeClip, 0.9f, 0.6f);
            }

            if (slamPending && Time.time >= slamArmedAt && player != null && player.IsGrounded)
                Slam();

            if (Time.time >= endTime) EndSuper();
        }

        private void Slam()
        {
            slamPending = false;
            Vector3 at = transform.position + Vector3.down * 0.8f;
            AoEDamage.Apply(at, slamRadius, slamDamage, gameObject);
            var chainOrigin = ChainLightning.NearestTarget(at, slamRadius, gameObject);
            if (chainOrigin != null) ChainLightning.Chain(chainOrigin, chainDamage, gameObject);
            FX.SpawnElementBurst(at, Element.Arc, 2f);

            // The ground stays angry for a few seconds.
            var aftershock = new GameObject("Aftershock").AddComponent<AftershockZone>();
            aftershock.transform.position = at;
            aftershock.Configure(aftershockDamagePerTick, slamRadius * 0.9f, aftershockSeconds, gameObject);
            SFX.Play3D(SFX.ArcShockClip, at, 1f, 12f);
            SFX.Play2D(SFX.GunshotFor(100f), 0.7f, 0.5f); // low crack for the quake
            if (player != null) player.AddRecoil(4f, 0.8f);
        }

        private void EndSuper()
        {
            active = false;
            slamPending = false;
            suppressKillEnergy = false;
            if (player != null) player.SpeedScale = 1f;
            if (weapon != null) weapon.SetHolstered(false);
            if (fistArcs != null)
            {
                Destroy(fistArcs.gameObject);
                fistArcs = null;
            }
        }
    }
}
