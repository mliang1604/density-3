using System.Collections;
using UnityEngine;
using Density3.Core;

namespace Density3.Enemies
{
    /// <summary>
    /// Wire-rifle sniper: holds long range, paints the player with a laser
    /// telegraph, then snaps a fast high-damage bolt down the line. The
    /// telegraph is the counterplay — the aim tracks until the final lock
    /// window (beam flares white), so breaking the line after lock makes
    /// the shot go wide.
    /// </summary>
    public class VandalEnemy : ChaserEnemy
    {
        [Header("Wire Rifle")]
        [Tooltip("Rifle tip the telegraph and bolt originate from (prefab reference).")]
        public Transform muzzle;
        public float telegraphSeconds = 1.2f;
        [Tooltip("Final stretch of the telegraph during which the aim is locked — the dodge window.")]
        public float lockSeconds = 0.25f;
        [Tooltip("Closer than this, the Vandal backpedals to reopen the gap.")]
        public float retreatRange = 22f;

        private bool telegraphing;
        private LineRenderer beam;

        /// <summary>Canonical Vandal tuning — shared by the bootstrap bake and
        /// the runtime fallback (ClassKits.Configure pattern).</summary>
        public static void Configure(EnemyData d)
        {
            d.displayName = "Vandal";
            d.maxHealth = 215f; // exactly three 140-frame crits (3 x 72)
            d.moveSpeed = 3.8f;
            d.strafeSpeed = 2.2f;
            d.aggroRange = 60f;
            d.preferredRange = 35f;
            d.fireRange = 50f;
            d.fireInterval = 4.5f;
            d.projectileDamage = 38f;
            d.projectileSpeed = 60f;
        }

        protected override EnemyData DefaultData()
        {
            var d = base.DefaultData();
            Configure(d);
            return d;
        }

        protected override Vector3 ComputeMove(Vector3 fwd, float dist)
        {
            if (telegraphing) return Vector3.zero; // planted for the shot
            if (dist < retreatRange) return fwd * (-0.8f * data.moveSpeed);
            return base.ComputeMove(fwd, dist);
        }

        protected override void Fire()
        {
            if (telegraphing) return;
            StartCoroutine(Telegraph());
        }

        private Vector3 MuzzlePos => muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * 0.7f + transform.forward * 1.1f;

        private Vector3 PlayerChest => player.position + Vector3.up * 0.5f;

        private IEnumerator Telegraph()
        {
            Vector3 aim = PlayerChest;
            // Player is on Ignore Raycast, so a hit means a wall blocks the lane.
            if (Physics.Linecast(MuzzlePos, aim)) yield break;

            telegraphing = true;
            beam = FX.SpawnBeam(MuzzlePos, aim, Element.Arc, 0.035f);
            SFX.Play3D(SFX.SniperChargeClip, transform.position, 0.7f, 10f);

            Color trackColor = ElementPalette.Base(Element.Arc);
            trackColor.a = 0.55f;
            Color lockColor = Color.Lerp(ElementPalette.Base(Element.Arc), Color.white, 0.8f);

            float t = 0f;
            while (t < telegraphSeconds)
            {
                if (health.IsDead || player == null || playerHealth == null || playerHealth.IsDead)
                {
                    EndTelegraph();
                    yield break;
                }

                bool locked = t >= telegraphSeconds - lockSeconds;
                if (!locked)
                {
                    aim = PlayerChest;
                    if (Physics.Linecast(MuzzlePos, aim)) // target broke line of sight
                    {
                        EndTelegraph();
                        yield break;
                    }
                }

                beam.SetPosition(0, MuzzlePos);
                beam.SetPosition(1, aim);
                Color c = locked ? lockColor : trackColor;
                beam.startColor = c;
                beam.endColor = c;
                beam.widthMultiplier = locked ? 1.8f : 1f;

                t += Time.deltaTime;
                yield return null;
            }

            // The shot is committed down the locked line — if the player broke
            // it, the bolt flies wide or slams into cover (its own raycast).
            Vector3 origin = MuzzlePos;
            Vector3 dir = (aim - origin).normalized;
            var proj = FX.SpawnBolt(origin, Element.Arc).AddComponent<EnemyProjectile>();
            proj.Launch(dir, playerHealth, data.projectileSpeed, data.projectileDamage);
            FX.AddElementTrail(proj.gameObject, Element.Arc, 0.18f, 0.22f);
            SFX.Play3D(SFX.ArcZapClip, origin, 0.9f, 12f, 1.3f);
            RaiseFired();
            EndTelegraph();
        }

        private void EndTelegraph()
        {
            telegraphing = false;
            if (beam != null) Destroy(beam.gameObject);
            beam = null;
        }
    }
}
