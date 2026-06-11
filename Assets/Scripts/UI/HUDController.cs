using UnityEngine;
using UnityEngine.UI;
using FableFPS.Core;
using FableFPS.Weapons;

namespace FableFPS.UI
{
    /// <summary>
    /// HUD: crosshair, shield bar, ammo counter, kill counter, reload indicator,
    /// damage vignette, respawn overlay. The layout lives in the HUD prefab
    /// (generated once by the editor bootstrap via BuildLayout, then freely
    /// editable); player/weapon references are resolved at runtime in Start.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Wiring (prefab references)")]
        public Text ammoText;
        public Text weaponNameText;
        public Text killsText;
        public Text reloadText;
        public Text respawnText;
        public Text hintText;
        public Image healthFill;
        public Image vignette;
        public GameObject crosshairRing;

        private HandCannon weapon;
        private Health playerHealth;
        private float vignetteAlpha;
        private int kills;
        private Font font;

        private void Start()
        {
            weapon = FindFirstObjectByType<HandCannon>();
            if (weapon != null) playerHealth = weapon.GetComponent<Health>();
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;

            // The ring sprite is procedural; regenerate if the prefab doesn't have one.
            if (crosshairRing != null)
            {
                var img = crosshairRing.GetComponent<Image>();
                if (img != null && img.sprite == null) img.sprite = MakeRingSprite(64, 26f, 3f);
            }
        }

        private void Update()
        {
            if (weapon != null && weapon.Current != null)
            {
                if (ammoText != null)
                    ammoText.text = weapon.RoundsInMag + "  |  ∞";
                if (weaponNameText != null)
                    weaponNameText.text = weapon.Current.displayName.ToUpperInvariant()
                        + "  ·  " + weapon.Current.frameName.ToUpperInvariant()
                        + "  ·  " + Mathf.RoundToInt(weapon.Current.roundsPerMinute) + " RPM";
                if (reloadText != null) reloadText.gameObject.SetActive(weapon.IsReloading);
                if (crosshairRing != null) crosshairRing.SetActive(!weapon.IsReloading);
            }

            if (playerHealth != null && healthFill != null)
            {
                float pct = Mathf.Clamp01(playerHealth.Current / playerHealth.MaxHealth);
                healthFill.rectTransform.sizeDelta = new Vector2(376f * pct, -4f);
                healthFill.color = pct < 0.3f
                    ? new Color(1f, 0.35f, 0.3f)
                    : new Color(0.92f, 0.96f, 1f, 0.95f);
            }

            if (vignette != null)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, 1.2f * Time.deltaTime);
                var vc = vignette.color;
                vc.a = vignetteAlpha;
                vignette.color = vc;
            }
        }

        public void ShowRespawnOverlay(bool show)
        {
            if (respawnText != null) respawnText.gameObject.SetActive(show);
        }

        private void OnPlayerDamaged(DamageInfo info) =>
            vignetteAlpha = Mathf.Min(0.5f, vignetteAlpha + 0.3f);

        private void OnEnemyKilled()
        {
            kills++;
            if (killsText != null) killsText.text = "KILLS  " + kills;
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
        }

        // ----- One-time layout construction (called by the editor bootstrap; the
        // result is saved into the HUD prefab and can be edited there) -----

        public void BuildLayout()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            Transform root = canvasGO.transform;

            vignette = MakeImage(root, "Vignette", new Color(0.7f, 0f, 0f, 0f));
            Stretch(vignette.rectTransform);

            var ring = MakeImage(root, "CrosshairRing", new Color(1f, 1f, 1f, 0.9f));
            SetCenter(ring.rectTransform, Vector2.zero, new Vector2(34f, 34f));
            crosshairRing = ring.gameObject;

            var dot = MakeImage(root, "CrosshairDot", new Color(1f, 1f, 1f, 0.95f));
            SetCenter(dot.rectTransform, Vector2.zero, new Vector2(3.5f, 3.5f));

            var hbBG = MakeImage(root, "HealthBG", new Color(0f, 0f, 0f, 0.55f));
            var bgRect = hbBG.rectTransform;
            bgRect.anchorMin = bgRect.anchorMax = new Vector2(0.5f, 1f);
            bgRect.pivot = new Vector2(0.5f, 1f);
            bgRect.anchoredPosition = new Vector2(0f, -24f);
            bgRect.sizeDelta = new Vector2(380f, 14f);

            healthFill = MakeImage(bgRect, "HealthFill", new Color(0.92f, 0.96f, 1f, 0.95f));
            var fillRect = healthFill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(2f, 0f);
            fillRect.sizeDelta = new Vector2(376f, -4f);

            weaponNameText = MakeText(root, "WeaponName", 22, TextAnchor.LowerRight);
            Anchor(weaponNameText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 96f), new Vector2(520f, 30f));

            ammoText = MakeText(root, "Ammo", 52, TextAnchor.LowerRight);
            Anchor(ammoText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(420f, 60f));

            hintText = MakeText(root, "Hint", 16, TextAnchor.LowerRight);
            Anchor(hintText.rectTransform, new Vector2(1f, 0f), new Vector2(-40f, 14f), new Vector2(620f, 22f));
            hintText.text = "[1][2][3] swap frame   [R] reload   [RMB] aim   [Shift] sprint   [C] crouch/slide   [Space] double jump";
            hintText.color = new Color(1f, 1f, 1f, 0.45f);

            killsText = MakeText(root, "Kills", 26, TextAnchor.UpperRight);
            Anchor(killsText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(300f, 34f));
            killsText.text = "KILLS  0";

            reloadText = MakeText(root, "Reload", 22, TextAnchor.MiddleCenter);
            SetCenter(reloadText.rectTransform, new Vector2(0f, -60f), new Vector2(300f, 30f));
            reloadText.text = "RELOADING";
            reloadText.color = new Color(1f, 0.8f, 0.3f, 0.9f);
            reloadText.gameObject.SetActive(false);

            respawnText = MakeText(root, "Respawn", 44, TextAnchor.MiddleCenter);
            SetCenter(respawnText.rectTransform, Vector2.zero, new Vector2(900f, 60f));
            respawnText.text = "GUARDIAN DOWN - RESPAWNING...";
            respawnText.color = new Color(1f, 0.35f, 0.3f, 1f);
            respawnText.gameObject.SetActive(false);
        }

        private Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text MakeText(Transform parent, string name, int size, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static Sprite MakeRingSprite(int size, float radius, float thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float c = size / 2f - 0.5f;
            float texRadius = radius * size / 34f * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - texRadius) / thickness);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
