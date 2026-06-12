using System.Collections;
using UnityEngine;
using Density3.Core;

namespace Density3.Encounter
{
    /// <summary>
    /// Health-gated immunity: when the boss falls to a gate's health
    /// fraction, its health clamps exactly at the threshold (burst damage
    /// cannot skip a gate), immunity raises, and the EncounterDirector
    /// pours in that gate's reinforcement wave. Clearing the wave drops
    /// immunity and starts the next phase. While a gate is open this
    /// component also backstops the direct-damage paths (ability AoE,
    /// DoT) by healing anything that slips through back to the clamp —
    /// single owner for all gate math, immune to event ordering.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(ImmunityShield))]
    public class BossGate : MonoBehaviour
    {
        [System.Serializable]
        public class Gate
        {
            [Range(0f, 1f)] public float healthFraction = 0.66f;
            public WaveData addWave;
        }

        public string bossName = "SIRIKS, LIGHT TURNED";
        public Gate[] gates;

        /// <summary>1-based: phase 1 on spawn, +1 as each gate breaks.</summary>
        public int Phase { get; private set; } = 1;
        public bool GateActive { get; private set; }

        /// <summary>Raised when immunity goes up (the brain retreats) and
        /// when a gate breaks into a new phase (the brain escalates).</summary>
        public event System.Action GateOpened;
        public event System.Action<int> PhaseStarted;

        private Health health;
        private ImmunityShield immunity;
        private EncounterDirector director;
        private int nextGate;
        private float activeThreshold;

        private void Awake()
        {
            health = GetComponent<Health>();
            immunity = GetComponent<ImmunityShield>();
            health.Damaged += OnDamaged;
        }

        private void Start()
        {
            // Cross-prefab reference: the director lives in the scene.
            director = FindFirstObjectByType<EncounterDirector>();
            GameEvents.AnnounceBossSpawned(health, bossName, GateFractions());
        }

        private void OnDestroy()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private float[] GateFractions()
        {
            var f = new float[gates != null ? gates.Length : 0];
            for (int i = 0; i < f.Length; i++) f[i] = gates[i].healthFraction;
            return f;
        }

        /// <summary>Runs inside Health.Damaged, before the death check — the
        /// same interceptor seam EnergyShield uses, so a gate can never be
        /// jumped by a killing blow.</summary>
        private void OnDamaged(DamageInfo info)
        {
            if (health.IsDead) return;

            if (GateActive)
            {
                // Hitbox paths already bounce off; this catches direct
                // ApplyDamage callers and holds the line at the clamp.
                if (health.Current < activeThreshold)
                    health.Heal(activeThreshold - health.Current);
                return;
            }

            if (gates == null || nextGate >= gates.Length) return;
            float threshold = gates[nextGate].healthFraction * health.MaxHealth;
            if (health.Current > threshold) return;

            health.Heal(threshold - health.Current); // clamp exactly at the gate
            StartCoroutine(RunGate(gates[nextGate]));
        }

        private IEnumerator RunGate(Gate gate)
        {
            GateActive = true;
            activeThreshold = gate.healthFraction * health.MaxHealth;
            immunity.SetImmune(true);
            GameEvents.AnnounceBossGateStarted();
            GateOpened?.Invoke();

            if (director != null)
            {
                director.SpawnSideWave(gate.addWave);
                yield return null; // let the side wave register before polling
                while (director.SideWaveActive)
                {
                    if (health.IsDead) yield break; // safety: gates shouldn't outlive the boss
                    yield return null;
                }
            }

            immunity.SetImmune(false);
            nextGate++;
            Phase = nextGate + 1;
            GateActive = false;
            SFX.Play2D(SFX.SuperActivateClip, 0.7f, 0.7f); // the phase sting
            GameEvents.AnnounceBossPhaseStarted(Phase);
            PhaseStarted?.Invoke(Phase);
        }
    }
}
