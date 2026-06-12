using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Density3.Core;
using Density3.Player;

namespace Density3.Encounter
{
    /// <summary>
    /// Runs a sequence of WaveData assets: spawns each wave's entries in
    /// parallel (per-entry stagger) from named spawn points, tracks every
    /// living spawn via its Health.Died, advances on wave-clear, and
    /// announces WaveStarted / WaveCleared / EncounterComplete through
    /// GameEvents. Spawned enemies have their respawn cycles switched off —
    /// mission deads stay dead and the corpses despawn themselves. Spawning
    /// halts for good when the player goes down.
    /// </summary>
    public class EncounterDirector : MonoBehaviour
    {
        public WaveData[] waves;
        [Tooltip("Parent of the named spawn-point transforms.")]
        public Transform spawnRoot;
        public bool autoStart = true;
        [Tooltip("Spawn this far above the point; gravity settles the rigs.")]
        public float spawnHeight = 2f;

        private readonly HashSet<Health> alive = new HashSet<Health>();
        private readonly HashSet<Health> sideAlive = new HashSet<Health>();
        private readonly Dictionary<Health, System.Action> deathHandlers
            = new Dictionary<Health, System.Action>();
        private Health playerHealth;
        private bool halted;
        private int sidePending;

        private void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                playerHealth = pc.GetComponent<Health>();
                if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            }
            if (autoStart) StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            foreach (var pair in deathHandlers)
                if (pair.Key != null) pair.Key.Died -= pair.Value;
            deathHandlers.Clear();
        }

        /// <summary>Player down = mission over; stop pouring enemies in.</summary>
        private void OnPlayerDied()
        {
            halted = true;
            StopAllCoroutines();
        }

        private IEnumerator Run()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                if (wave == null) continue;
                yield return new WaitForSeconds(wave.startDelay);

                GameEvents.AnnounceWaveStarted(i + 1, waves.Length);

                int pending = wave.entries != null ? wave.entries.Length : 0;
                if (wave.entries != null)
                    foreach (var entry in wave.entries)
                        StartCoroutine(SpawnEntry(entry, () => pending--));

                while (pending > 0 || alive.Count > 0) yield return null;

                GameEvents.AnnounceWaveCleared(i + 1);
            }
            GameEvents.AnnounceEncounterComplete();
        }

        private IEnumerator SpawnEntry(WaveData.SpawnEntry entry, System.Action done, bool side = false)
        {
            var point = ResolvePoint(entry.spawnPoint);
            for (int n = 0; n < entry.count; n++)
            {
                if (entry.enemyPrefab != null) Spawn(entry.enemyPrefab, point, side);
                if (n < entry.count - 1) yield return new WaitForSeconds(entry.stagger);
            }
            done();
        }

        // ----- Side waves: boss-gate reinforcements outside the main sequence -----

        /// <summary>True while a side wave has spawns incoming or alive —
        /// boss gates hold immunity until this clears.</summary>
        public bool SideWaveActive => sidePending > 0 || sideAlive.Count > 0;

        /// <summary>Runs one wave outside the main sequence. Side spawns also
        /// join the main alive set, so the encounter can't complete while
        /// gate reinforcements still breathe.</summary>
        public void SpawnSideWave(WaveData wave)
        {
            if (wave == null || halted) return;
            StartCoroutine(RunSideWave(wave));
        }

        private IEnumerator RunSideWave(WaveData wave)
        {
            sidePending++;
            yield return new WaitForSeconds(wave.startDelay);
            int pending = wave.entries != null ? wave.entries.Length : 0;
            if (wave.entries != null)
                foreach (var entry in wave.entries)
                    StartCoroutine(SpawnEntry(entry, () => pending--, side: true));
            while (pending > 0) yield return null;
            sidePending--;
        }

        private Transform ResolvePoint(string pointName)
        {
            Transform point = spawnRoot != null ? spawnRoot.Find(pointName) : null;
            if (point == null)
            {
                var go = GameObject.Find(pointName);
                if (go != null) point = go.transform;
            }
            if (point == null)
            {
                Debug.LogWarning("EncounterDirector: no spawn point named '" + pointName + "'");
                point = transform;
            }
            return point;
        }

        private void Spawn(GameObject prefab, Transform point, bool side = false)
        {
            Vector3 pos = point.position + Vector3.up * spawnHeight;
            var go = Instantiate(prefab, pos, point.rotation);

            // Mission enemies die for real: their respawn cycles become
            // corpse-despawns instead.
            var dregDeath = go.GetComponent<DregDeath>();
            if (dregDeath != null) dregDeath.respawn = false;
            var respawner = go.GetComponent<Respawner>();
            if (respawner != null) respawner.respawn = false;

            var health = go.GetComponent<Health>();
            if (health != null)
            {
                alive.Add(health);
                if (side) sideAlive.Add(health);
                System.Action handler = () => OnSpawnDied(health);
                deathHandlers[health] = handler;
                health.Died += handler;
            }

            // Fallen arrive in a crack of arc light.
            FX.SpawnElementBurst(pos, Element.Arc, 1.1f);
            SFX.Play3D(SFX.ArcZapClip, pos, 0.6f, 8f, 0.8f);
        }

        private void OnSpawnDied(Health health)
        {
            alive.Remove(health);
            sideAlive.Remove(health);
            if (deathHandlers.TryGetValue(health, out var handler))
            {
                health.Died -= handler;
                deathHandlers.Remove(health);
            }
        }

        /// <summary>Living spawn count — boss phases (M7) read this for
        /// add-clear gating.</summary>
        public int AliveCount => halted ? 0 : alive.Count;
    }
}
