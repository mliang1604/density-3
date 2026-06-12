using System.Collections;
using UnityEngine;
using Density3.Abilities;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.Core
{
    /// <summary>Handles player death/respawn and global audio prewarm. Scene
    /// references are serialized; anything missing is found at Start.</summary>
    public class GameManager : MonoBehaviour
    {
        /// <summary>Class picked on the title screen; survives the scene load.
        /// Warlock when a gameplay scene is launched directly in the editor.</summary>
        public static GuardianClass SelectedClass = GuardianClass.Warlock;

        public float respawnDelay = 3f;
        [Tooltip("Off in mission scenes: death belongs to the MissionController, not a free respawn.")]
        public bool freeRespawn = true;

        [Header("Wiring (scene references)")]
        public PlayerController player;
        public HandCannon weapon;
        public HUDController hud;

        [Header("Audio")]
        [Tooltip("Optional real gunshot recording. When set, replaces the synthesized gunshots (pitched per frame). Clear to fall back to synthesis.")]
        public AudioClip gunshotRecording;

        private Health playerHealth;
        private Vector3 spawnPos;
        private Quaternion spawnRot;

        private void Awake()
        {
            SFX.Prewarm();
            SFX.SetRecordedGunshot(gunshotRecording);
        }

        private void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            if (weapon == null) weapon = player.GetComponent<HandCannon>();
            if (hud == null) hud = FindFirstObjectByType<HUDController>();

            // Player prefabs that predate the ability system get the stack at
            // runtime (ClassLoadout adds PlayerAbilities itself if missing).
            if (player.GetComponent<ClassLoadout>() == null)
                player.gameObject.AddComponent<ClassLoadout>();

            playerHealth = player.GetComponent<Health>();
            spawnPos = player.transform.position;
            spawnRot = player.transform.rotation;
            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
                playerHealth.Damaged += OnPlayerDamaged;
            }
        }

        private void OnPlayerDied()
        {
            if (freeRespawn) StartCoroutine(RespawnRoutine());
        }

        private void OnPlayerDamaged(DamageInfo info) => SFX.Play2D(SFX.PlayerHurtClip, 0.7f);

        private IEnumerator RespawnRoutine()
        {
            player.MovementLocked = true;
            if (hud != null) hud.ShowRespawnOverlay(true);
            yield return new WaitForSeconds(respawnDelay);

            CharacterTeleport.To(player.transform, spawnPos, spawnRot);
            player.ResetLook(spawnRot.eulerAngles.y);

            playerHealth.Revive();
            if (weapon != null) weapon.RefillMag();
            if (hud != null) hud.ShowRespawnOverlay(false);
            player.MovementLocked = false;
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
                playerHealth.Damaged -= OnPlayerDamaged;
            }
        }
    }
}
