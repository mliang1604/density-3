using System.Collections.Generic;
using UnityEngine;
using Density3.Core;

namespace Density3.Enemies
{
    /// <summary>
    /// Siriks, Light Turned — the Zero Hour boss, built on the Captain brain
    /// and driven by a three-phase state machine keyed off the BossGate:
    ///
    ///  Phase 1 (100-66%): anchored artillery. Holds a baked anchor, throws
    ///    five-bolt volleys, and blinks between anchors on a cadence — or
    ///    the moment the player gets too close. No melee.
    ///  Phase 2 (66-33%): the Captain's advance — closes to arm's reach
    ///    behind shorter volleys and heavy claw swipes.
    ///  Phase 3 (33-0%): enrage. Faster everything: movement, volleys of
    ///    seven, quicker and harder swipes.
    ///
    /// While a gate holds it immune, Siriks blinks home to the vault door
    /// and stands burning — no shots, no swipes — until the adds fall.
    /// Anchors resolve by name at runtime (prefabs can't hold scene refs).
    /// </summary>
    public class SiriksEnemy : CaptainEnemy
    {
        [Header("Anchors (scene transforms, resolved by name)")]
        public string[] anchorNames = { "BossAnchor", "Spawn_FloorNE", "Spawn_FloorNW" };
        [Tooltip("Phase 1 relocation cadence.")]
        public float blinkInterval = 12f;
        [Tooltip("Phase 1: blink away the moment the player closes inside this.")]
        public float blinkPanicRange = 9f;
        [Tooltip("Root height over an anchor's floor point (CC bottom just clear).")]
        public float anchorHeight = 1.85f;
        public Color lightColor = new Color(1f, 0.95f, 0.75f);

        [Header("Enrage (phase 3)")]
        public float enrageMoveScale = 1.45f;
        public float enrageFireScale = 0.55f;
        public int enrageVolleyBolts = 7;
        public float enrageMeleeInterval = 1.1f;
        public float enrageMeleeDamage = 33f;

        private Transform[] anchors = new Transform[0];
        private int anchorIndex;
        private float nextBlink;
        private int phase = 1;
        private Encounter.BossGate bossGate;
        private ImmunityShield immunity;

        /// <summary>Canonical Siriks tuning — shared by the bootstrap bake and
        /// the runtime fallback (ClassKits.Configure pattern).</summary>
        public static new void Configure(EnemyData d)
        {
            d.displayName = "Siriks";
            d.maxHealth = 2000f; // boss pool, no regen
            d.moveSpeed = 4.6f;
            d.strafeSpeed = 2.2f;
            d.aggroRange = 70f;  // the whole vault room
            d.preferredRange = 7f;
            d.fireRange = 45f;
            d.fireInterval = 4f;
            d.projectileDamage = 7f; // per bolt; volleys are the threat
            d.projectileSpeed = 22f;
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
            immunity = GetComponent<ImmunityShield>();
            bossGate = GetComponent<Encounter.BossGate>();
            if (bossGate != null)
            {
                bossGate.GateOpened += OnGateOpened;
                bossGate.PhaseStarted += OnPhaseStarted;
            }
        }

        private void OnDestroy()
        {
            if (bossGate != null)
            {
                bossGate.GateOpened -= OnGateOpened;
                bossGate.PhaseStarted -= OnPhaseStarted;
            }
        }

        protected override void Start()
        {
            base.Start();
            ResolveAnchors();
            nextBlink = Time.time + blinkInterval;
        }

        private void ResolveAnchors()
        {
            var found = new List<Transform>();
            foreach (var anchorName in anchorNames)
            {
                var go = GameObject.Find(anchorName);
                if (go != null) found.Add(go.transform);
                else Debug.LogWarning("SiriksEnemy: no anchor named '" + anchorName + "'");
            }
            anchors = found.ToArray();
        }

        private bool Immune => immunity != null && immunity.Immune;

        /// <summary>Gate up: blink home to the vault door and stand burning.</summary>
        private void OnGateOpened()
        {
            if (anchors.Length > 0)
            {
                anchorIndex = 0;
                BlinkTo(anchors[0]);
            }
        }

        private void OnPhaseStarted(int newPhase)
        {
            phase = newPhase;
            if (phase >= 3)
            {
                // Enrage: the stolen Light burns through everything it does.
                fireIntervalScale = enrageFireScale;
                volleyBolts = enrageVolleyBolts;
                meleeInterval = enrageMeleeInterval;
                meleeDamage = enrageMeleeDamage;
                FX.SpawnColorBurst(transform.position + Vector3.up * 2.2f, lightColor, 2.2f);
            }
            else if (phase == 2)
            {
                fireIntervalScale = 0.85f;
                volleyBolts = 3; // shorter volleys while advancing
            }
        }

        protected override Vector3 ComputeMove(Vector3 fwd, float dist)
        {
            if (Immune) return Vector3.zero; // planted at the vault, burning

            if (phase == 1)
            {
                // Anchored artillery: walk back onto the anchor if displaced.
                if (anchors.Length == 0) return base.ComputeMove(fwd, dist);
                Vector3 to = anchors[anchorIndex].position + Vector3.up * anchorHeight
                             - transform.position;
                to.y = 0f;
                return to.magnitude > 1.2f ? to.normalized * data.moveSpeed : Vector3.zero;
            }

            // Phases 2/3: the Captain's aggressive advance, faster when enraged.
            Vector3 move = base.ComputeMove(fwd, dist);
            return phase >= 3 ? move * enrageMoveScale : move;
        }

        protected override bool ReadyToFire(float dist)
            => !Immune && base.ReadyToFire(dist);

        protected override void Tick(Vector3 fwd, float dist)
        {
            if (Immune) return; // no swipes, no blinks — the gate owns this beat

            if (phase == 1)
            {
                // Artillery keeps its distance: relocate on cadence, or the
                // moment the player closes in. No melee this phase.
                if (anchors.Length > 1 && (Time.time >= nextBlink || dist < blinkPanicRange))
                    BlinkAwayFromPlayer();
                return;
            }

            base.Tick(fwd, dist); // the Captain's claw swipe
        }

        /// <summary>Relocate to the anchor farthest from the player.</summary>
        private void BlinkAwayFromPlayer()
        {
            if (player == null) return;
            int best = anchorIndex;
            float bestDist = -1f;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (i == anchorIndex) continue;
                float d = (anchors[i].position - player.position).sqrMagnitude;
                if (d > bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            anchorIndex = best;
            BlinkTo(anchors[best]);
        }

        /// <summary>Teleport with a stolen-Light flash at both ends.</summary>
        private void BlinkTo(Transform anchor)
        {
            FX.SpawnColorBurst(transform.position + Vector3.up * 1.5f, lightColor, 1.5f);
            CharacterTeleport.To(transform, anchor.position + Vector3.up * anchorHeight, FacingPlayer());
            FX.SpawnColorBurst(transform.position + Vector3.up * 1.5f, lightColor, 1.5f);
            SFX.Play3D(SFX.ArcZapClip, transform.position, 0.9f, 12f, 0.7f);
            nextBlink = Time.time + blinkInterval;
        }

        private Quaternion FacingPlayer()
        {
            if (player == null) return transform.rotation;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.normalized) : transform.rotation;
        }
    }
}
