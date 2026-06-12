using UnityEngine;

namespace Density3.UI
{
    /// <summary>Destiny-style floating damage numbers (white body, yellow crit,
    /// element-tinted while the damage lands on an energy shield).</summary>
    public static class DamageNumbers
    {
        private static Font font;

        public static void Spawn(Vector3 pos, float amount, bool crit)
            => Spawn(pos, amount, crit, crit ? new Color(1f, 0.85f, 0.25f) : Color.white);

        public static void Spawn(Vector3 pos, float amount, bool crit, Color color)
            => SpawnText(pos, Mathf.RoundToInt(amount).ToString(), color, crit ? 0.05f : 0.035f);

        /// <summary>The boss-gate readout: hits during immunity say so.</summary>
        public static void SpawnImmune(Vector3 pos)
            => SpawnText(pos, "IMMUNE", new Color(0.78f, 0.78f, 0.85f), 0.026f);

        public static void SpawnText(Vector3 pos, string text, Color color, float charSize)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("DamageNumber");
            go.transform.position = pos + Random.insideUnitSphere * 0.15f;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().material = font.material;

            go.AddComponent<FloatingNumber>();
        }
    }

    public class FloatingNumber : MonoBehaviour
    {
        private const float Life = 0.7f;

        private float t;
        private Vector3 drift;
        private TextMesh tm;

        private void Awake()
        {
            tm = GetComponent<TextMesh>();
            drift = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.2f), 0f);
        }

        private void LateUpdate()
        {
            t += Time.deltaTime;
            transform.position += drift * Time.deltaTime;

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            float a = 1f - Mathf.Clamp01((t - Life * 0.5f) / (Life * 0.5f));
            var c = tm.color;
            c.a = a;
            tm.color = c;

            if (t >= Life) Destroy(gameObject);
        }
    }
}
