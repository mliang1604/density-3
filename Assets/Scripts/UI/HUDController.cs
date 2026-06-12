using System;
using UnityEngine;
using UnityEngine.UI;
using Density3.Abilities;
using Density3.Core;
using Density3.Player;
using Density3.Weapons;

namespace Density3.UI
{
    /// <summary>
    /// HUD: crosshair, shield bar, ammo counter, kill counter, FPS counter,
    /// reload indicator, damage vignette, respawn overlay. The layout lives in the HUD prefab
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
        public Text fpsText;
        public Image healthFill;
        public Image vignette;
        public Image superFill;
        public Image grenadeFill;
        public Image meleeFill;
        public Image classFill;
        [HideInInspector] public RectTransform[] glyphRoots = new RectTransform[4];
        public GameObject crosshairRing;

        private HandCannon weapon;
        private Health playerHealth;
        private PlayerController playerController;
        private float vignetteAlpha;
        private int kills;
        private bool jumpHintSet;
        private Font font;
        private int fpsFrames;
        private float fpsTimer;

        private const float SuperSize = 56f;
        private const float AbilitySize = 34f;
        private PlayerAbilities abilities;
        private bool abilitiesBound;
        private Color abilityTint = new Color(0.92f, 0.96f, 1f, 0.95f);
        private readonly float[] readyFlash = new float[4]; // super, grenade, melee, class
        private AbilityBase[] boundAbilities;
        private Action<bool>[] readyHandlers;
        private Color vignetteBaseColor = new Color(0.7f, 0f, 0f);
        private Sprite iconRingSprite;

        private void Start()
        {
            weapon = FindFirstObjectByType<HandCannon>();
            if (weapon != null)
            {
                playerHealth = weapon.GetComponent<Health>();
                playerController = weapon.GetComponent<PlayerController>();
            }
            if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.WaveStarted += OnWaveStarted;
            GameEvents.WaveCleared += OnWaveCleared;

            if (vignette != null) vignetteBaseColor = vignette.color;

            // The ring sprite is procedural; regenerate if the prefab doesn't have one.
            if (crosshairRing != null)
            {
                var img = crosshairRing.GetComponent<Image>();
                if (img != null && img.sprite == null) img.sprite = MakeRingSprite(64, 26f, 3f);
            }

            // HUD prefabs predating the FPS counter don't have one wired; build it.
            if (fpsText == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas != null) MakeFpsText(canvas.transform);
            }

            // Same fallback for prefabs predating the ability meters.
            if (superFill == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas != null) MakeAbilityMeters(canvas.transform);
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

            // Ability meters bind lazily: ClassLoadout fills the slots in Awake
            // (possibly added during GameManager.Start), so the first Update —
            // which runs after every Start — can always bind.
            if (!abilitiesBound) TryBindAbilities();
            UpdateAbilityMeters();

            UpdateBanner();

            // Averaging over a short window keeps the readout from flickering.
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                if (fpsText != null) fpsText.text = "FPS  " + Mathf.RoundToInt(fpsFrames / fpsTimer);
                fpsFrames = 0;
                fpsTimer = 0f;
            }

            // The jump style is fixed per class; set the hint once.
            if (hintText != null && playerController != null && !jumpHintSet)
            {
                jumpHintSet = true;
                hintText.text = HintText(
                    playerController.jumpStyle == JumpStyle.Glide ? "glide"
                    : playerController.jumpStyle == JumpStyle.TripleJump ? "triple jump"
                    : playerController.jumpStyle == JumpStyle.Jetpack ? "jetpack"
                    : "strafe jump");
            }
        }

        /// <summary>The control-hint line; the [Space] entry names the active jump type.</summary>
        private static string HintText(string jumpName) =>
            "[1][2][3] swap frame   [R] reload   [RMB] aim   [Shift] sprint   [C] crouch/slide"
            + "   [Space] " + jumpName + "   [hold Backspace] exit";

        public void ShowRespawnOverlay(bool show)
        {
            if (respawnText != null) respawnText.gameObject.SetActive(show);
        }

        // ----- Wave banner (runtime-built: committed HUD prefabs need no rebake) -----

        private Text bannerText;
        private float bannerUntil;

        private void OnWaveStarted(int number, int total)
            => ShowBanner("WAVE  " + number + "  /  " + total, new Color(0.92f, 0.96f, 1f, 0.95f));

        private void OnWaveCleared(int number)
            => ShowBanner("WAVE  CLEARED", new Color(1f, 0.85f, 0.4f, 0.95f));

        /// <summary>Large center-screen announcement that holds, then fades.</summary>
        public void ShowBanner(string message, Color color, float seconds = 2.2f)
        {
            if (bannerText == null)
            {
                var canvas = GetComponentInChildren<Canvas>();
                if (canvas == null) return;
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                bannerText = MakeText(canvas.transform, "Banner", 40, TextAnchor.MiddleCenter);
                SetCenter(bannerText.rectTransform, new Vector2(0f, 160f), new Vector2(900f, 52f));
            }
            bannerText.text = message;
            bannerText.color = color;
            bannerUntil = Time.time + seconds;
            bannerText.gameObject.SetActive(true);
        }

        private void UpdateBanner()
        {
            if (bannerText == null || !bannerText.gameObject.activeSelf) return;
            float left = bannerUntil - Time.time;
            if (left <= 0f)
            {
                bannerText.gameObject.SetActive(false);
                return;
            }
            var c = bannerText.color;
            c.a = Mathf.Clamp01(left / 0.5f); // hold, then a half-second fade
            bannerText.color = c;
        }

        /// <summary>One-off colored vignette pulse (super casts and the like).
        /// The next damage pulse restores the standard red.</summary>
        public void PulseVignette(Color tint, float alpha)
        {
            if (vignette == null) return;
            tint.a = vignette.color.a;
            vignette.color = tint;
            vignetteAlpha = Mathf.Max(vignetteAlpha, alpha);
        }

        private void OnPlayerDamaged(DamageInfo info)
        {
            if (vignette != null)
            {
                Color c = vignetteBaseColor;
                c.a = vignette.color.a;
                vignette.color = c;
            }
            vignetteAlpha = Mathf.Min(0.5f, vignetteAlpha + 0.3f);
        }

        private void OnEnemyKilled()
        {
            kills++;
            if (killsText != null) killsText.text = "KILLS  " + kills;
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.WaveStarted -= OnWaveStarted;
            GameEvents.WaveCleared -= OnWaveCleared;
            if (boundAbilities != null)
                for (int i = 0; i < boundAbilities.Length; i++)
                    if (boundAbilities[i] != null && readyHandlers[i] != null)
                        boundAbilities[i].ReadyChanged -= readyHandlers[i];
        }

        private void TryBindAbilities()
        {
            if (abilities == null) abilities = FindFirstObjectByType<PlayerAbilities>();
            if (abilities == null) return;

            var loadout = abilities.GetComponent<ClassLoadout>();
            if (loadout != null && loadout.Active != null)
                abilityTint = Color.Lerp(
                    ElementPalette.Base(loadout.Active.element), Color.white, 0.25f);

            // Slot glyphs are class-specific (knife vs palm, dodge vs rift),
            // so they're built here, once the class is known.
            var guardianClass = loadout != null && loadout.Active != null
                ? loadout.Active.guardianClass : GuardianClass.Warlock;
            for (int i = 0; i < glyphRoots.Length; i++)
                if (glyphRoots[i] != null && glyphRoots[i].childCount == 0)
                    BuildSlotGlyph(glyphRoots[i], i, guardianClass);

            boundAbilities = new[]
                { abilities.super, abilities.grenade, abilities.melee, abilities.classAbility };
            readyHandlers = new Action<bool>[boundAbilities.Length];
            for (int i = 0; i < boundAbilities.Length; i++)
            {
                if (boundAbilities[i] == null) continue;
                int slot = i;
                readyHandlers[i] = ready => OnAbilityReady(slot, ready);
                boundAbilities[i].ReadyChanged += readyHandlers[i];
            }
            abilitiesBound = true;
        }

        private void OnAbilityReady(int slot, bool ready)
        {
            if (!ready) return;
            readyFlash[slot] = 1f;
            // The super landing is the bigger moment — deeper, louder chime.
            SFX.Play2D(SFX.AbilityReadyClip, slot == 0 ? 0.55f : 0.35f, slot == 0 ? 0.8f : 1.2f);
        }

        private void UpdateAbilityMeters()
        {
            SetMeter(superFill, 0, SuperSize);
            SetMeter(grenadeFill, 1, AbilitySize);
            SetMeter(meleeFill, 2, AbilitySize);
            SetMeter(classFill, 3, AbilitySize);
        }

        private void SetMeter(Image fill, int slot, float iconSize)
        {
            if (fill == null) return;
            float energy = boundAbilities != null && boundAbilities[slot] != null
                ? boundAbilities[slot].Energy : 0f;
            readyFlash[slot] = Mathf.MoveTowards(readyFlash[slot], 0f, 2f * Time.deltaTime);

            // Destiny-style: the icon fills bottom-to-top while charging, dim,
            // then goes bright at full; the ready moment also flashes white.
            fill.rectTransform.sizeDelta = new Vector2(-4f, (iconSize - 4f) * energy);
            float brightness = energy >= 1f ? 1f : 0.55f;
            var c = new Color(abilityTint.r * brightness, abilityTint.g * brightness,
                abilityTint.b * brightness, abilityTint.a);
            fill.color = Color.Lerp(c, Color.white, readyFlash[slot]);
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
            hintText.text = HintText("strafe jump");
            hintText.color = new Color(1f, 1f, 1f, 0.45f);

            killsText = MakeText(root, "Kills", 26, TextAnchor.UpperRight);
            Anchor(killsText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(300f, 34f));
            killsText.text = "KILLS  0";

            MakeFpsText(root);
            MakeAbilityMeters(root);

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

        /// <summary>Destiny-style bottom-left ability cluster: a large diamond
        /// super icon beside a row of square grenade/melee/class icons, each
        /// filling bottom-to-top with energy (dim while charging, bright when
        /// ready) and carrying its key bind as the glyph. Called from
        /// BuildLayout and, for prefabs that predate it, from Start.</summary>
        private void MakeAbilityMeters(Transform root)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            superFill = MakeAbilityIcon(root, "Super", "Q", new Vector2(72f, 78f), SuperSize, 45f, 0);
            grenadeFill = MakeAbilityIcon(root, "Grenade", "G", new Vector2(140f, 78f), AbilitySize, 0f, 1);
            meleeFill = MakeAbilityIcon(root, "Melee", "V", new Vector2(184f, 78f), AbilitySize, 0f, 2);
            classFill = MakeAbilityIcon(root, "Class", "F", new Vector2(228f, 78f), AbilitySize, 0f, 3);
        }

        private Image MakeAbilityIcon(Transform root, string name, string key, Vector2 center,
            float size, float rotation, int glyphSlot)
        {
            var bg = MakeImage(root, name + "BG", new Color(0f, 0f, 0f, 0.55f));
            var bgRect = bg.rectTransform;
            bgRect.anchorMin = bgRect.anchorMax = new Vector2(0f, 0f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = center;
            bgRect.sizeDelta = new Vector2(size, size);
            bgRect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            // The fill grows upward inside the icon — through the rotated
            // super diamond that reads as point-to-point, like D2's.
            var fill = MakeImage(bgRect, name + "Fill", new Color(0.92f, 0.96f, 1f, 0.95f));
            var fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 0f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.anchoredPosition = new Vector2(0f, 2f);
            fr.sizeDelta = new Vector2(-4f, 0f);

            // Key bind sits below the icon (clear of the diamond's point).
            float half = rotation != 0f ? size * 0.7071f : size * 0.5f;
            var keyLabel = MakeText(root, name + "Key", 11, TextAnchor.MiddleCenter);
            keyLabel.text = key;
            keyLabel.color = new Color(1f, 1f, 1f, 0.55f);
            var kr = keyLabel.rectTransform;
            kr.anchorMin = kr.anchorMax = new Vector2(0f, 0f);
            kr.pivot = new Vector2(0.5f, 0.5f);
            kr.anchoredPosition = center + new Vector2(0f, -(half + 9f));
            kr.sizeDelta = new Vector2(40f, 14f);

            // Slot glyph container, counter-rotated upright, drawn over the
            // fill. Left empty here: glyphs are class-specific and get built
            // by TryBindAbilities once the loadout is known.
            var glyphGO = new GameObject(name + "Icon", typeof(RectTransform));
            var gr = glyphGO.GetComponent<RectTransform>();
            gr.SetParent(bgRect, false);
            gr.anchorMin = Vector2.zero;
            gr.anchorMax = Vector2.one;
            gr.offsetMin = Vector2.zero;
            gr.offsetMax = Vector2.zero;
            gr.localRotation = Quaternion.Euler(0f, 0f, -rotation);
            glyphRoots[glyphSlot] = gr;
            return fill;
        }

        /// <summary>Tiny procedural slot icons per class — no art assets, same
        /// philosophy as the crosshair sprite. Slot order: super, grenade,
        /// melee, class ability.</summary>
        private void BuildSlotGlyph(RectTransform parent, int slot, GuardianClass guardianClass)
        {
            if (iconRingSprite == null) iconRingSprite = MakeRingSprite(64, 26f, 4f);
            var c = new Color(1f, 1f, 1f, 0.92f);

            if (guardianClass == GuardianClass.Titan)
            {
                switch (slot)
                {
                    case 0: // Fists of Havoc: twin fists over a cracked ground line
                        GlyphBar(parent, new Vector2(7f, 9f), 0f, c, new Vector2(-5f, 4f));
                        GlyphBar(parent, new Vector2(7f, 9f), 0f, c, new Vector2(5f, 4f));
                        GlyphBar(parent, new Vector2(22f, 2.5f), 0f, c, new Vector2(0f, -6f));
                        break;
                    case 1: // Pulse: concentric shocks
                        GlyphRing(parent, new Vector2(10f, 10f), c);
                        GlyphRing(parent, new Vector2(18f, 18f), c);
                        break;
                    case 2: // Seismic Strike: a fist with speed lines
                        GlyphBar(parent, new Vector2(7f, 7f), 0f, c, new Vector2(4f, 0f));
                        GlyphBar(parent, new Vector2(8f, 2f), 0f, c, new Vector2(-5f, 3f));
                        GlyphBar(parent, new Vector2(8f, 2f), 0f, c, new Vector2(-5f, -3f));
                        break;
                    case 3: // Barricade: wall planks
                        GlyphBar(parent, new Vector2(4.5f, 14f), 0f, c, new Vector2(-6f, 0f));
                        GlyphBar(parent, new Vector2(4.5f, 14f), 0f, c);
                        GlyphBar(parent, new Vector2(4.5f, 14f), 0f, c, new Vector2(6f, 0f));
                        break;
                }
                return;
            }

            if (guardianClass == GuardianClass.Hunter)
            {
                switch (slot)
                {
                    case 0: // Golden Gun: a pistol silhouette
                        GlyphBar(parent, new Vector2(16f, 4f), 0f, c, new Vector2(2f, 4f));
                        GlyphBar(parent, new Vector2(4.5f, 11f), 20f, c, new Vector2(-5f, -3f));
                        break;
                    case 1: // Tripmine: a mine and its laser
                        GlyphBar(parent, new Vector2(6f, 6f), 45f, c, new Vector2(-6f, 0f));
                        GlyphBar(parent, new Vector2(13f, 2f), 0f, c, new Vector2(4f, 0f));
                        break;
                    case 2: // Throwing knife: blade and guard
                        GlyphBar(parent, new Vector2(2.8f, 16f), 45f, c);
                        GlyphBar(parent, new Vector2(9f, 2.5f), -45f, c, new Vector2(-4f, -4f));
                        break;
                    case 3: // Dodge: dash lines
                        GlyphBar(parent, new Vector2(14f, 2.5f), 0f, c, new Vector2(2f, 5f));
                        GlyphBar(parent, new Vector2(11f, 2.5f), 0f, c, new Vector2(-1f, 0f));
                        GlyphBar(parent, new Vector2(8f, 2.5f), 0f, c, new Vector2(-4f, -5f));
                        break;
                }
                return;
            }

            switch (slot)
            {
                case 0: // super: a nova burst
                    GlyphBar(parent, new Vector2(26f, 2.5f), 0f, c);
                    GlyphBar(parent, new Vector2(26f, 2.5f), 60f, c);
                    GlyphBar(parent, new Vector2(26f, 2.5f), 120f, c);
                    break;
                case 1: // grenade: an orb
                    GlyphRing(parent, new Vector2(16f, 16f), c);
                    var dot = MakeImage(parent, "Dot", c);
                    SetCenter(dot.rectTransform, Vector2.zero, new Vector2(5f, 5f));
                    break;
                case 2: // melee: claw slashes
                    GlyphBar(parent, new Vector2(18f, 2.5f), 45f, c, new Vector2(-2.5f, 2.5f));
                    GlyphBar(parent, new Vector2(18f, 2.5f), 45f, c, new Vector2(2.5f, -2.5f));
                    break;
                case 3: // class ability: a rift ellipse
                    var ring = GlyphRing(parent, new Vector2(20f, 20f), c);
                    ring.rectTransform.localScale = new Vector3(1f, 0.5f, 1f);
                    break;
            }
        }

        private void GlyphBar(RectTransform parent, Vector2 size, float rot, Color c, Vector2 pos = default)
        {
            var img = MakeImage(parent, "Bar", c);
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            r.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        private Image GlyphRing(RectTransform parent, Vector2 size, Color c)
        {
            var img = MakeImage(parent, "Ring", c);
            img.sprite = iconRingSprite;
            SetCenter(img.rectTransform, Vector2.zero, size);
            return img;
        }

        /// <summary>Top-left FPS readout, mirroring the kill counter's placement.
        /// Called from BuildLayout and, for prefabs that predate it, from Start.</summary>
        private void MakeFpsText(Transform root)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fpsText = MakeText(root, "FPS", 26, TextAnchor.UpperLeft);
            Anchor(fpsText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(300f, 34f));
            fpsText.text = "FPS  --";
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
