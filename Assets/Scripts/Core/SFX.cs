using UnityEngine;

namespace FableFPS.Core
{
    /// <summary>
    /// Procedurally synthesized sound effects — no audio files needed.
    /// Clips are generated once on first use and played through a small
    /// pool of 2D sources (weapon/UI) or PlayClipAtPoint (world sounds).
    /// </summary>
    public static class SFX
    {
        private const int SampleRate = 44100;

        private static AudioClip gunshotHeavy;
        private static AudioClip gunshotMedium;
        private static AudioClip gunshotLight;
        private static AudioClip dryFire;
        private static AudioClip reloadStart;
        private static AudioClip reloadEnd;
        private static AudioClip critHit;
        private static AudioClip bodyHit;
        private static AudioClip playerHurt;
        private static AudioClip boltFire;
        private static AudioClip boltImpact;
        private static AudioClip doubleJump;
        private static AudioClip enemyDeath;
        private static AudioClip etherBurst;
        private static AudioClip slide;

        public static AudioClip DryFireClip { get { EnsureClips(); return dryFire; } }
        public static AudioClip ReloadStartClip { get { EnsureClips(); return reloadStart; } }
        public static AudioClip ReloadEndClip { get { EnsureClips(); return reloadEnd; } }
        public static AudioClip CritHitClip { get { EnsureClips(); return critHit; } }
        public static AudioClip BodyHitClip { get { EnsureClips(); return bodyHit; } }
        public static AudioClip PlayerHurtClip { get { EnsureClips(); return playerHurt; } }
        public static AudioClip BoltFireClip { get { EnsureClips(); return boltFire; } }
        public static AudioClip BoltImpactClip { get { EnsureClips(); return boltImpact; } }
        public static AudioClip DoubleJumpClip { get { EnsureClips(); return doubleJump; } }
        public static AudioClip EnemyDeathClip { get { EnsureClips(); return enemyDeath; } }
        public static AudioClip EtherBurstClip { get { EnsureClips(); return etherBurst; } }
        public static AudioClip SlideClip { get { EnsureClips(); return slide; } }

        public static AudioClip GunshotFor(float rpm)
        {
            EnsureClips();
            if (rpm <= 130f) return gunshotHeavy;
            if (rpm < 170f) return gunshotMedium;
            return gunshotLight;
        }

        // Optional real recording that replaces the synthesized gunshots
        // (registered by GameManager from an Inspector-assigned AudioClip).
        private static AudioClip recordedGunshot;

        public static void SetRecordedGunshot(AudioClip clip) => recordedGunshot = clip;

        /// <summary>
        /// Fires the gunshot sound for a frame. Uses the recorded clip when one
        /// is registered — pitched per frame so the 120/140/180 still feel
        /// different from one sample — otherwise the synthesized fallbacks.
        /// </summary>
        public static void PlayGunshot(float rpm, float volume = 0.8f)
        {
            if (recordedGunshot != null)
            {
                float framePitch = rpm <= 130f ? 0.82f : rpm < 170f ? 1f : 1.18f;
                Play2D(recordedGunshot, volume, framePitch * Random.Range(0.97f, 1.03f));
            }
            else
            {
                Play2D(GunshotFor(rpm), volume, Random.Range(0.96f, 1.04f));
            }
        }

        /// <summary>Generate all clips and the source pool up front (avoids first-shot hitch).</summary>
        public static void Prewarm()
        {
            EnsureClips();
            NextSource();
        }

        // ----- Playback ----------------------------------------------------

        private static AudioSource[] pool;
        private static int poolIndex;

        public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var src = NextSource();
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        /// <summary>Positioned one-shot. minDistance sets how far the sound carries
        /// at full volume — big values (5-10) for sounds that must read clearly
        /// at combat range, 1 for quiet local effects.</summary>
        public static void Play3D(AudioClip clip, Vector3 pos, float volume = 1f, float minDistance = 1f)
        {
            if (clip == null) return;
            var go = new GameObject("SFX3D");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.Play();
            Object.Destroy(go, clip.length + 0.1f);
        }

        private static AudioSource NextSource()
        {
            if (pool == null || pool.Length == 0 || pool[0] == null)
            {
                var go = new GameObject("SFX2D");
                pool = new AudioSource[8];
                for (int i = 0; i < pool.Length; i++)
                {
                    var src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;
                    pool[i] = src;
                }
            }
            poolIndex = (poolIndex + 1) % pool.Length;
            return pool[poolIndex];
        }

        // ----- Synthesis ---------------------------------------------------

        private static void EnsureClips()
        {
            if (gunshotMedium != null) return;
            gunshotHeavy = BuildGunshot("sfx_shot_heavy", 0.7f, 140f, 700f, 1.3f, 1.6f, 0.09f, 1.15f);
            gunshotMedium = BuildGunshot("sfx_shot_medium", 0.55f, 180f, 900f, 1.0f, 1.7f, 0.075f, 1f);
            gunshotLight = BuildGunshot("sfx_shot_light", 0.45f, 220f, 1100f, 0.8f, 1.85f, 0.06f, 0.92f);
            dryFire = BuildClick("sfx_dry", 0.05f, 1800f, 0.35f);
            reloadStart = BuildClick("sfx_reload_start", 0.07f, 900f, 0.5f);
            reloadEnd = BuildClick("sfx_reload_end", 0.09f, 650f, 0.6f);
            critHit = BuildDing("sfx_crit", 1320f, 1980f, 0.16f, 0.05f, 0.7f);
            bodyHit = BuildDing("sfx_body", 280f, 420f, 0.08f, 0.03f, 0.6f);
            playerHurt = BuildThump("sfx_hurt", 75f, 0.25f, 0.09f, 0.04f, 0.4f, 0.9f);
            boltImpact = BuildThump("sfx_bolt_impact", 300f, 0.15f, 0.04f, 0.025f, 0.8f, 0.6f);
            boltFire = BuildZap("sfx_bolt_fire", 0.2f, 0.7f);
            doubleJump = BuildWhoosh("sfx_double_jump", 0.3f, 0.8f);
            enemyDeath = BuildDeathPop("sfx_enemy_death", 0.35f, 0.9f);
            etherBurst = BuildEther("sfx_ether_burst", 1.5f, 1.05f);
            slide = BuildWhoosh("sfx_slide", 0.6f, 0.7f);
        }

        /// <summary>
        /// Precision-kill ether burst: the head pops, then pressurized gas screams
        /// out — a falling, wobbling whistle over a band-passed hiss bed, loudest
        /// in the first instant (peak pressure), audible for about a second, and
        /// faded to silence by the end of the clip.
        /// </summary>
        private static AudioClip BuildEther(string name, float duration, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f, phase2 = 0f;
            float hissHi = 0f, hissLo = 0f;
            float aHi = Alpha(6000f), aLo = Alpha(1800f);

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float t01 = t / duration;

                // Pressure envelope: extra burst at the start, slow bleed-off,
                // forced to silence over the last ~40% of the clip.
                float fade = 1f - Mathf.SmoothStep(0.6f, 0.98f, t01);
                float env = (1f + 1.5f * Mathf.Exp(-t / 0.08f)) * Mathf.Exp(-t / 1.2f) * fade;

                // The pop of the head letting go.
                float pop = Rand(rng) * Mathf.Exp(-t / 0.012f) * 1.2f;

                // Screaming whistle: pitch falls as pressure drops, with vibrato
                // that grows more unstable toward the end.
                float wobble = (0.02f + 0.06f * t01) * Mathf.Sin(2f * Mathf.PI * 31f * t)
                               + 0.02f * Mathf.Sin(2f * Mathf.PI * 7.3f * t);
                float f = Mathf.Lerp(2600f, 800f, Mathf.Pow(t01, 0.7f)) * (1f + wobble);
                phase += 2f * Mathf.PI * f / SampleRate;
                phase2 += 2f * Mathf.PI * f * 1.013f / SampleRate; // detuned partner
                float scream = (Mathf.Sin(phase) + 0.6f * Mathf.Sin(phase2)) * 0.38f;

                // Gas hiss: band-passed noise under the whistle.
                float white = Rand(rng);
                hissHi += (white - hissHi) * aHi;
                hissLo += (white - hissLo) * aLo;
                float hiss = (hissHi - hissLo) * 0.85f;

                data[i] = SoftClip((pop + (scream + hiss) * env) * gain);
            }
            return MakeClip(name, data);
        }

        /// <summary>
        /// D2-style hand cannon report: a tight, dry CRACK built entirely from
        /// shaped noise bands (no tonal sine layers — those read as drums):
        ///  - crack: 1-5 kHz band, ~18 ms decay — the whip-snap of the shot
        ///  - body: mid band (per frame), ~60 ms — short and punchy, not boomy
        ///  - thump: low-passed noise below ~130 Hz, ~90 ms — weight without pitch
        ///  - ring: two faint damped high partials — the metallic frame edge
        ///  - tail: down-sweeping filtered noise, ~160 ms — quick outdoor decay
        /// plus a single quiet slapback echo, like range-recorded reference audio.
        /// </summary>
        private static AudioClip BuildGunshot(string name, float duration, float bodyLowHz,
            float bodyHighHz, float lowAmp, float crackAmp, float echoDelay, float gain)
        {
            int n = (int)(SampleRate * duration);
            var raw = new float[n];
            var rng = new System.Random(name.GetHashCode());

            float aCrackHi = Alpha(5000f), aCrackLo = Alpha(1000f);
            float aBodyHi = Alpha(bodyHighHz), aBodyLo = Alpha(bodyLowHz);
            float aLow = Alpha(130f);
            float crackHi = 0f, crackLo = 0f, bodyHi = 0f, bodyLo = 0f, low = 0f, tailLp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float white = Rand(rng);

                // Band-pass = difference of two one-pole low-passes.
                crackHi += (white - crackHi) * aCrackHi;
                crackLo += (white - crackLo) * aCrackLo;
                float crack = (crackHi - crackLo) * Mathf.Exp(-t / 0.018f) * crackAmp;

                bodyHi += (white - bodyHi) * aBodyHi;
                bodyLo += (white - bodyLo) * aBodyLo;
                float body = (bodyHi - bodyLo) * Mathf.Exp(-t / 0.06f) * 1.5f;

                low += (white - low) * aLow;
                float thump = low * Mathf.Exp(-t / 0.09f) * lowAmp * 2.2f;

                float ring = (Mathf.Sin(2f * Mathf.PI * 2380f * t)
                              + 0.7f * Mathf.Sin(2f * Mathf.PI * 3710f * t))
                             * Mathf.Exp(-t / 0.05f) * 0.07f;

                float tailCut = Mathf.Lerp(900f, 200f, Mathf.Clamp01(t / 0.2f));
                tailLp += (white - tailLp) * Alpha(tailCut);
                float tail = tailLp * Mathf.Exp(-t / 0.16f) * 0.7f;

                raw[i] = crack + body + thump + ring + tail;
            }

            // Single quiet slapback echo, then limit.
            int delay = (int)(echoDelay * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = raw[i];
                if (i >= delay) s += raw[i - delay] * 0.22f;
                data[i] = SoftClip(s * gain * 1.4f);
            }
            return MakeClip(name, data);
        }

        private static float Alpha(float cutoffHz) =>
            1f - Mathf.Exp(-2f * Mathf.PI * cutoffHz / SampleRate);

        private static AudioClip BuildClick(string name, float duration, float freq, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                phase += 2f * Mathf.PI * freq / SampleRate;
                float tone = Mathf.Sin(phase) * Mathf.Exp(-t / 0.012f);
                float noise = Rand(rng) * Mathf.Exp(-t / 0.006f) * 0.7f;
                data[i] = (tone + noise) * gain;
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildDing(string name, float f1, float f2, float duration, float decayTau, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            float p1 = 0f, p2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                p1 += 2f * Mathf.PI * f1 / SampleRate;
                p2 += 2f * Mathf.PI * f2 / SampleRate;
                data[i] = (Mathf.Sin(p1) * 0.7f + Mathf.Sin(p2) * 0.4f) * Mathf.Exp(-t / decayTau) * gain;
            }
            return MakeClip(name, data);
        }

        private static AudioClip BuildThump(string name, float freq, float duration, float toneTau,
            float noiseTau, float noiseAmp, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                phase += 2f * Mathf.PI * freq / SampleRate;
                float tone = Mathf.Sin(phase) * Mathf.Exp(-t / toneTau);
                float noise = Rand(rng) * Mathf.Exp(-t / noiseTau) * noiseAmp;
                data[i] = SoftClip((tone + noise) * gain);
            }
            return MakeClip(name, data);
        }

        /// <summary>Wobbling harmonic stack for enemy energy bolts.</summary>
        private static AudioClip BuildZap(string name, float duration, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = 160f + 40f * Mathf.Sin(2f * Mathf.PI * 18f * t);
                phase += 2f * Mathf.PI * f / SampleRate;
                float s = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.3f + Mathf.Sin(phase * 3f) * 0.15f;
                float attack = Mathf.Min(1f, t / 0.005f);
                data[i] = s * attack * Mathf.Exp(-t / 0.07f) * gain;
            }
            return MakeClip(name, data);
        }

        /// <summary>Low-passed noise with a swell envelope (double-jump boost).</summary>
        private static AudioClip BuildWhoosh(string name, float duration, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t01 = (float)i / n;
                lp += (Rand(rng) - lp) * 0.08f;
                data[i] = lp * Mathf.Sin(t01 * Mathf.PI) * gain * 2.5f;
            }
            return MakeClip(name, data);
        }

        /// <summary>Descending sweep + noise burst for enemy deaths.</summary>
        private static AudioClip BuildDeathPop(string name, float duration, float gain)
        {
            int n = (int)(SampleRate * duration);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float f = Mathf.Lerp(420f, 120f, (float)i / n);
                phase += 2f * Mathf.PI * f / SampleRate;
                float tone = Mathf.Sin(phase) * Mathf.Exp(-t / 0.12f);
                float noise = Rand(rng) * Mathf.Exp(-t / 0.05f) * 0.5f;
                data[i] = SoftClip((tone + noise) * gain);
            }
            return MakeClip(name, data);
        }

        private static float Rand(System.Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);

        private static float SoftClip(float x) => (float)System.Math.Tanh(x * 1.7f);

        private static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
