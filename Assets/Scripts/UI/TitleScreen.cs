using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Density3.Core;

namespace Density3.UI
{
    /// <summary>
    /// Title screen: pulses the "press to play" prompt, lets 1/2/3 pick the
    /// guardian class (carried into gameplay via GameManager.SelectedClass),
    /// and loads the game scene on Enter. The cursor is freed so the title
    /// feels like a menu.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public string gameSceneName = "TestRange";
        public Text pressToPlay;
        public Text classSelect;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Title scenes baked before class selection build the line at runtime.
            if (classSelect == null) classSelect = BuildClassSelectText();
            RefreshClassText();
        }

        private void Update()
        {
            if (pressToPlay != null)
            {
                var c = pressToPlay.color;
                c.a = 0.55f + 0.35f * Mathf.Sin(Time.time * 2.5f);
                pressToPlay.color = c;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) Select(GuardianClass.Warlock);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Select(GuardianClass.Hunter);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Select(GuardianClass.Titan);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SceneManager.LoadScene(gameSceneName);
        }

        private void Select(GuardianClass g)
        {
            if (GameManager.SelectedClass == g) return;
            GameManager.SelectedClass = g;
            RefreshClassText();
            SFX.Play2D(SFX.ReloadStartClip, 0.35f, 1.1f + 0.15f * (int)g);
        }

        private void RefreshClassText()
        {
            if (classSelect == null) return;
            classSelect.text = Entry(GuardianClass.Warlock, "1") + "        "
                + Entry(GuardianClass.Hunter, "2") + "        "
                + Entry(GuardianClass.Titan, "3");
        }

        /// <summary>The selected class glows in its element color.</summary>
        private static string Entry(GuardianClass g, string key)
        {
            string label = "[" + key + "]  " + g.ToString().ToUpperInvariant();
            if (GameManager.SelectedClass != g) return label;
            string hex = ColorUtility.ToHtmlStringRGB(ElementPalette.Base(g.ElementOf()));
            return "<color=#" + hex + ">" + label + "</color>";
        }

        private Text BuildClassSelectText()
        {
            var go = new GameObject("ClassSelect");
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 22;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.93f, 0.88f, 0.78f);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rect = t.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 200f);
            rect.sizeDelta = new Vector2(1100f, 30f);
            return t;
        }
    }
}
