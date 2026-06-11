using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Density3.UI
{
    /// <summary>
    /// Title screen: pulses the "press to play" prompt and loads the game
    /// scene on Enter. The cursor is freed so the title feels like a menu.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public string gameSceneName = "TestRange";
        public Text pressToPlay;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (pressToPlay != null)
            {
                var c = pressToPlay.color;
                c.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 2.5f);
                pressToPlay.color = c;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SceneManager.LoadScene(gameSceneName);
        }
    }
}
