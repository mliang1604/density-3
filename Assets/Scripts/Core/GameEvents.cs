using System;
using UnityEngine;

namespace Density3.Core
{
    public static class GameEvents
    {
        public static event Action EnemyKilled;

        /// <summary>Encounter beats published by the EncounterDirector:
        /// (waveNumber, totalWaves) on start, waveNumber on clear, and the
        /// final all-waves-down moment that mission flow treats as the win.</summary>
        public static event Action<int, int> WaveStarted;
        public static event Action<int> WaveCleared;
        public static event Action EncounterComplete;

        /// <summary>Boss beats: spawn (the HUD bar binds to the Health and
        /// marks the gate fractions as pips), gate openings (immunity up,
        /// reinforcements incoming), and each new phase as a gate breaks.</summary>
        public static event Action<Health, string, float[]> BossSpawned;
        public static event Action BossGateStarted;
        public static event Action<int> BossPhaseStarted;

        /// <summary>Announce an enemy kill: plays the death sting at the corpse
        /// and raises EnemyKilled, so kill feedback stays in sync everywhere.</summary>
        public static void AnnounceEnemyKilled(Vector3 position)
        {
            SFX.Play3D(SFX.EnemyDeathClip, position, 0.9f);
            EnemyKilled?.Invoke();
        }

        public static void AnnounceWaveStarted(int number, int total) => WaveStarted?.Invoke(number, total);

        public static void AnnounceWaveCleared(int number) => WaveCleared?.Invoke(number);

        public static void AnnounceEncounterComplete() => EncounterComplete?.Invoke();

        public static void AnnounceBossSpawned(Health health, string name, float[] gateFractions)
            => BossSpawned?.Invoke(health, name, gateFractions);

        public static void AnnounceBossGateStarted() => BossGateStarted?.Invoke();

        public static void AnnounceBossPhaseStarted(int phase) => BossPhaseStarted?.Invoke(phase);
    }
}
