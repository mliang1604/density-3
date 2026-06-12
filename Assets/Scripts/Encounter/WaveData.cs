using UnityEngine;

namespace Density3.Encounter
{
    /// <summary>
    /// One wave of an encounter: a list of spawn entries, each naming an
    /// enemy prefab, a count, a named spawn-point transform in the scene,
    /// and the stagger between spawns. Entries run in parallel — a wave
    /// can pour Dregs in from the left while Shanks lift off a walkway.
    /// </summary>
    [CreateAssetMenu(menuName = "Density3/Wave Data", fileName = "NewWaveData")]
    public class WaveData : ScriptableObject
    {
        [System.Serializable]
        public class SpawnEntry
        {
            public GameObject enemyPrefab;
            public int count = 1;
            [Tooltip("Name of a transform under the scene's SpawnPoints root.")]
            public string spawnPoint = "Spawn_VaultL";
            [Tooltip("Seconds between consecutive spawns of this entry.")]
            public float stagger = 0.7f;
        }

        public string displayName = "Wave";
        [Tooltip("Pause before this wave's first spawn.")]
        public float startDelay = 2.5f;
        public SpawnEntry[] entries;
    }
}
