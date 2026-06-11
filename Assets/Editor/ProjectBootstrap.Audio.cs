using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Density3.Core;
using Density3.Enemies;
using Density3.Player;
using Density3.UI;
using Density3.Weapons;

namespace Density3.EditorTools
{
    public static partial class ProjectBootstrap
    {
        // ----- Audio import: trim one shot out of a range recording ---------------

        /// <summary>
        /// Cuts the first gunshot out of Assets/Audio/RevolverRaw.mp3 (a multi-shot
        /// range recording), saves it as Assets/Audio/HandCannonShot.wav, removes
        /// the raw file and any old placeholder clip, and rebuilds the scene so the
        /// GameManager points at the new clip.
        /// </summary>
        [MenuItem("Density3/Import Trimmed Revolver Recording")]
        public static void ImportRevolverRecording()
        {
            const string rawPath = "Assets/Audio/RevolverRaw.mp3";
            const string outPath = "Assets/Audio/HandCannonShot.wav";

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(rawPath);
            if (clip == null)
            {
                Debug.LogError("Density3: no clip at " + rawPath);
                return;
            }

            int channels = clip.channels;
            var interleaved = new float[clip.samples * channels];
            if (!clip.GetData(interleaved, 0))
            {
                Debug.LogError("Density3: couldn't read samples from " + rawPath);
                return;
            }

            // Mono mixdown.
            int n = clip.samples;
            var mono = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += interleaved[i * channels + c];
                mono[i] = sum / channels;
            }

            int sr = clip.frequency;
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(mono[i]));
            if (peak < 0.01f)
            {
                Debug.LogError("Density3: recording appears to be silent.");
                return;
            }

            // First transient = shot onset; start a hair before it.
            int onset = 0;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(mono[i]) > peak * 0.3f) { onset = i; break; }
            int start = Mathf.Max(0, onset - sr * 5 / 1000);

            // End before the next shot if one exists, else cap at 1.5 s of tail.
            int nextOnset = -1;
            for (int i = onset + (int)(0.3f * sr); i < n; i++)
                if (Mathf.Abs(mono[i]) > peak * 0.45f) { nextOnset = i; break; }
            int endLimit = nextOnset > 0 ? nextOnset - (int)(0.06f * sr) : n;
            int end = Mathf.Min(start + (int)(1.5f * sr), endLimit);
            if (end <= start + sr / 10)
            {
                Debug.LogError("Density3: trim window came out too small — onset detection failed.");
                return;
            }

            int len = end - start;
            var cut = new float[len];
            System.Array.Copy(mono, start, cut, 0, len);

            // Short fade-in (click guard) and fade-out (clean tail).
            int fadeIn = Mathf.Min(64, len / 10);
            for (int i = 0; i < fadeIn; i++) cut[i] *= (float)i / fadeIn;
            int fadeOut = Mathf.Min((int)(0.08f * sr), len / 4);
            for (int i = 0; i < fadeOut; i++) cut[len - 1 - i] *= (float)i / fadeOut;

            // Normalize to a consistent game volume (quiet field recordings
            // otherwise get lost under the synthesized SFX).
            float cutPeak = 0f;
            for (int i = 0; i < len; i++) cutPeak = Mathf.Max(cutPeak, Mathf.Abs(cut[i]));
            if (cutPeak > 0.001f)
            {
                float norm = 0.9f / cutPeak;
                for (int i = 0; i < len; i++) cut[i] *= norm;
            }

            WriteWav(System.IO.Path.GetFullPath(outPath), cut, sr);
            AssetDatabase.DeleteAsset("Assets/Audio/HandCannonShot.mp3");
            AssetDatabase.DeleteAsset(rawPath);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"Density3: trimmed single shot — onset {(float)onset / sr:F2}s, length {(float)len / sr:F2}s -> {outPath}");

            BuildAll(); // rewire the scene's GameManager to the new clip
        }

        private static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var bw = new System.IO.BinaryWriter(fs))
            {
                int dataLen = samples.Length * 2;
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(36 + dataLen);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1);       // PCM
                bw.Write((short)1);       // mono
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2); // byte rate
                bw.Write((short)2);       // block align
                bw.Write((short)16);      // bits per sample
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(dataLen);
                foreach (float s in samples)
                    bw.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32760f));
            }
        }
    }
}
