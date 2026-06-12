using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.UI;

namespace Density3.Enemies
{
    /// <summary>
    /// Kamikaze Shank: beelines at the player (a preferredRange of 0
    /// collapses the orbit band) behind an accelerating warning beep, and
    /// detonates within arm's reach. Shot down first, it still blows — but
    /// that blast is harmless to the player and wrecks nearby Fallen and
    /// barricades, so popping it early inside a pack is the play.
    /// EnemyData.projectileDamage doubles as the blast damage, keeping the
    /// number in the baked balance table.
    /// </summary>
    public class ExploderShankEnemy : ShankEnemy
    {
        [Header("Detonation")]
        [Tooltip("Distance to the player's chest that trips the contact fuse.")]
        public float detonateRange = 2f;
        public float blastRadius = 4.5f;

        [Header("Warning Glow")]
        public Color glowColor = new Color(1f, 0.5f, 0.1f);
        public float glowRange = 6f;

        private float nextBeep;
        private bool detonated;
        private Light glow;
        private float glowPhase;

        // The blast is event-driven and self-contained: one overlap, one
        // DamageInfo per distinct Health, no dependency on ability helpers.
        private static readonly Collider[] blastOverlaps = new Collider[32];
        private static readonly HashSet<Health> blastSeen = new HashSet<Health>();

        /// <summary>Canonical Exploder tuning — shared by the bootstrap bake
        /// and the runtime fallback (ClassKits.Configure pattern).</summary>
        public static new void Configure(EnemyData d)
        {
            d.displayName = "Exploder Shank";
            d.maxHealth = 60f;
            d.moveSpeed = 7.5f;
            d.strafeSpeed = 0f;
            d.aggroRange = 45f;
            d.preferredRange = 0f;  // no orbit band: always closing
            d.fireRange = 0f;       // never shoots — it IS the shot
            d.fireInterval = 2.2f;
            d.projectileDamage = 80f; // blast damage (player and enemies alike)
            d.projectileSpeed = 0f;
        }

        protected override EnemyData DefaultData()
        {
            var d = ScriptableObject.CreateInstance<EnemyData>();
            Configure(d);
            return d;
        }

        protected override void Awake()
        {
            base.Awake();

            // Built at runtime so committed prefabs glow without a rebake.
            var go = new GameObject("WarningGlow");
            go.transform.SetParent(transform, false);
            glow = go.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = glowColor;
            glow.range = glowRange;
        }

        /// <summary>Orange warning pulse, beating faster as it closes — the
        /// visual twin of the beep ramp. The Respawner only toggles renderers
        /// and colliders, so the light minds its own death state.</summary>
        private void LateUpdate()
        {
            if (glow == null) return;
            if (health == null || health.IsDead)
            {
                glow.enabled = false;
                return;
            }
            glow.enabled = true;

            float urgency = 0f;
            if (player != null)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                urgency = Mathf.InverseLerp(25f, 3f, dist);
            }
            glowPhase += Mathf.Lerp(2.5f, 14f, urgency) * Time.deltaTime;
            glow.intensity = 1.2f + (0.8f + 1.6f * urgency) * (0.5f + 0.5f * Mathf.Sin(glowPhase));
        }

        protected override void Tick(float dist)
        {
            Vector3 chest = player.position + Vector3.up * 0.5f;
            if ((transform.position - chest).sqrMagnitude <= detonateRange * detonateRange)
            {
                Detonate(true);
                return;
            }

            // The beep gets faster and shriller as it closes — the warning is
            // the counterplay window.
            if (Time.time >= nextBeep)
            {
                float t = Mathf.InverseLerp(25f, 3f, dist);
                nextBeep = Time.time + Mathf.Lerp(0.9f, 0.15f, t);
                SFX.Play3D(SFX.BeepClip, transform.position, 0.65f, 6f, Mathf.Lerp(0.9f, 1.7f, t));
            }
        }

        /// <summary>Shot down before contact: sympathetic detonation that
        /// spares the player. The kill announcement happens here because the
        /// Respawner's countsAsKill is off — a contact self-destruct is not
        /// a player kill.</summary>
        protected override void OnDied()
        {
            bool selfDestruct = detonated;
            Detonate(false);
            if (!selfDestruct) GameEvents.AnnounceEnemyKilled(transform.position);
        }

        protected override void OnRevived()
        {
            detonated = false;
            nextBeep = 0f;
        }

        private void Detonate(bool hurtPlayer)
        {
            if (detonated) return;
            detonated = true;

            Vector3 pos = transform.position;
            // Solar-orange blast — the payoff matches the warning glow.
            FX.SpawnElementBurst(pos, Element.Solar, 1.8f);
            SFX.Play3D(SFX.ArcShockClip, pos, 1f, 12f, 0.85f);

            float damage = data.projectileDamage;
            int n = Physics.OverlapSphereNonAlloc(pos, blastRadius, blastOverlaps, ~0,
                QueryTriggerInteraction.Ignore);
            blastSeen.Clear();
            for (int i = 0; i < n; i++)
            {
                var h = blastOverlaps[i].GetComponentInParent<Health>();
                if (h == null || h == health || h.IsDead || !blastSeen.Add(h)) continue;
                if (h == playerHealth && !hurtPlayer) continue;

                Vector3 at = blastOverlaps[i].ClosestPoint(pos);
                h.ApplyDamage(new DamageInfo
                {
                    amount = damage,
                    isCrit = false,
                    hitPoint = at,
                    source = gameObject
                });
                if (h != playerHealth) DamageNumbers.Spawn(at, damage, false);
            }

            // The contact fuse consumes the drone; the Respawner takes it from here.
            if (!health.IsDead)
                health.ApplyDamage(new DamageInfo
                {
                    amount = health.MaxHealth * 2f,
                    isCrit = false,
                    hitPoint = pos,
                    source = gameObject
                });
        }
    }
}
