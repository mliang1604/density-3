using UnityEngine;

namespace FableFPS.UI
{
    /// <summary>Destiny-style floating damage numbers (white body, yellow crit).</summary>
    public static class DamageNumbers
    {
        private static Font font;

        public static void Spawn(Vector3 pos, float amount, bool crit)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var go = new GameObject("DamageNumber");
            go.transform.position = pos + Random.insideUnitSphere * 0.15f;

            var tm = go.AddComponent<TextMesh>();
            tm.text = Mathf.RoundToInt(amount).ToString();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = crit ? 0.05f : 0.035f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = crit ? new Color(1f, 0.85f, 0.25f) : Color.white;
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
