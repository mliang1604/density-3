using UnityEngine;
using UnityEngine.SceneManagement;
using Density3.Core;
using Density3.Player;
using Density3.UI;

namespace Density3.Encounter
{
    /// <summary>
    /// Mission flow for Zero Hour: player death is a mission fail — the
    /// GameManager's free respawn is switched off in this scene — and the
    /// director's EncounterComplete is the win. Each end state locks the
    /// player, raises a full-screen overlay, and routes on Enter: fail
    /// reloads the mission scene for a clean retry, win returns to Title.
    /// </summary>
    public class MissionController : MonoBehaviour
    {
        public string titleSceneName = "Title";

        [Header("Countdown")]
        [Tooltip("Off for untimed testing — the HUD readout never appears.")]
        public bool timerEnabled = true;
        [Tooltip("Mission clock in seconds; expiry is a fail.")]
        public float missionSeconds = 600f;

        private enum State { Playing, Failed, Won }

        private State state = State.Playing;
        private Health playerHealth;
        private PlayerController player;
        private HUDController hud;
        private float remaining;
        private int lastTickSecond = -1;

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null) playerHealth = player.GetComponent<Health>();
            hud = FindFirstObjectByType<HUDController>();

            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            GameEvents.EncounterComplete += OnEncounterComplete;

            remaining = missionSeconds;
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
            GameEvents.EncounterComplete -= OnEncounterComplete;
        }

        private void Update()
        {
            if (state == State.Playing)
            {
                TickTimer();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (state == State.Failed)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // full reset
                else
                    SceneManager.LoadScene(titleSceneName);
            }
        }

        /// <summary>The Zero Hour clock: counts down on the HUD, goes red and
        /// audibly ticks through the final minute, fails the mission at zero.</summary>
        private void TickTimer()
        {
            if (!timerEnabled) return;
            remaining -= Time.deltaTime;
            bool urgent = remaining <= 60f;
            if (hud != null) hud.SetMissionTimer(Mathf.Max(0f, remaining), urgent);

            if (urgent && remaining > 0f)
            {
                int second = Mathf.CeilToInt(remaining);
                if (second != lastTickSecond)
                {
                    lastTickSecond = second;
                    // The last ten seconds tick higher and harder.
                    SFX.Play2D(SFX.TimerTickClip, second <= 10 ? 0.6f : 0.4f, second <= 10 ? 1.35f : 1f);
                }
            }
            if (remaining <= 0f) Fail("ZERO HOUR EXPIRED");
        }

        private void OnPlayerDied() => Fail("GUARDIAN DOWN");

        /// <summary>Mission fail with a stated reason — death here, timer
        /// expiry via the countdown.</summary>
        public void Fail(string reason)
        {
            if (state != State.Playing) return;
            state = State.Failed;
            LockPlayer();
            if (hud != null)
                hud.ShowMissionOverlay("MISSION  FAILED", reason + "      [Enter]  Retry",
                    new Color(1f, 0.32f, 0.28f));
        }

        private void OnEncounterComplete()
        {
            if (state != State.Playing) return;
            state = State.Won;
            LockPlayer();
            SFX.Play2D(SFX.SuperActivateClip, 0.6f, 1.1f); // triumphant swell
            if (hud != null)
                hud.ShowMissionOverlay("ZERO  HOUR  COMPLETE", "[Enter]  Return to Title",
                    new Color(1f, 0.85f, 0.4f));
        }

        private void LockPlayer()
        {
            if (player != null) player.MovementLocked = true;
        }
    }
}
