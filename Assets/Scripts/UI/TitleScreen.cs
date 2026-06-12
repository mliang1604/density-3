using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Density3.Core;

namespace Density3.UI
{
    /// <summary>
    /// Title screen: pulses the "press to play" prompt, lets 1/2/3 pick the
    /// guardian class (carried into gameplay via GameManager.SelectedClass),
    /// Tab toggle the destination (Zero Hour or the Test Range), and Enter
    /// launch. The cursor is freed so the title feels like a menu.
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public string gameSceneName = "TestRange";
        public string zeroHourSceneName = "ZeroHour";
        public Text pressToPlay;
        public Text classSelect;
        public Text destinationSelect;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Title scenes baked before these lines existed build them at runtime.
            if (classSelect == null) classSelect = BuildClassSelectText();
            if (destinationSelect == null) destinationSelect = BuildDestinationText();
            RefreshClassText();
            RefreshDestinationText();
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
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleDestination();

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SceneManager.LoadScene(GameManager.SelectedDestination == Destination.ZeroHour
                    ? zeroHourSceneName : gameSceneName);
        }

        private void ToggleDestination()
        {
            GameManager.SelectedDestination =
                GameManager.SelectedDestination == Destination.ZeroHour
                    ? Destination.TestRange : Destination.ZeroHour;
            RefreshDestinationText();
            SFX.Play2D(SFX.ReloadStartClip, 0.35f, 0.9f);
        }

        private void RefreshDestinationText()
        {
            if (destinationSelect == null) return;
            destinationSelect.text = "[Tab]  DESTINATION:      "
                + DestinationEntry(Destination.ZeroHour, "ZERO HOUR") + "        "
                + DestinationEntry(Destination.TestRange, "TEST RANGE");
        }

        /// <summary>The selected destination glows gold.</summary>
        private static string DestinationEntry(Destination d, string label)
        {
            if (GameManager.SelectedDestination != d) return label;
            return "<color=#FFD966>" + label + "</color>";
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

        private Text BuildClassSelectText() => BuildMenuLine("ClassSelect", 200f);

        private Text BuildDestinationText() => BuildMenuLine("DestinationSelect", 250f);

        private Text BuildMenuLine(string name, float y)
        {
            var go = new GameObject(name);
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
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(1100f, 30f);
            return t;
        }
    }
}
