using UnityEngine;

namespace Density3.Core
{
    /// <summary>
    /// Global exit-to-desktop control, present in every scene. On the title
    /// screen a tap quits; in gameplay it requires a hold, so the Esc tap
    /// that merely frees the cursor can't kill the app. (In WebGL builds
    /// Application.Quit is a no-op — browsers own the tab.)
    /// </summary>
    public class QuitHandler : MonoBehaviour
    {
        public KeyCode quitKey = KeyCode.Escape;
        public bool requireHold;
        public float holdSeconds = 1.2f;

        private float heldFor;

        private void Update()
        {
            if (!requireHold)
            {
                if (Input.GetKeyDown(quitKey)) Quit();
                return;
            }

            if (Input.GetKey(quitKey))
            {
                heldFor += Time.deltaTime;
                if (heldFor >= holdSeconds) Quit();
            }
            else
            {
                heldFor = 0f;
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
