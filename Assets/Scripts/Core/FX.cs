using UnityEngine;
using UnityEngine.Rendering;

namespace Density3.Core
{
    /// <summary>Cheap procedural effects: bullet tracers and impact sparks.</summary>
    public static class FX
    {
        private static Material lineMaterial;
        private static Material impactMaterial;

        public static void SpawnTracer(Vector3 start, Vector3 end, Color color)
        {
            var go = new GameObject("Tracer");
            var lr = go.AddComponent<LineRenderer>();
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.material = lineMaterial;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = 0.025f;
            lr.endWidth = 0.01f;
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0.15f);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            Object.Destroy(go, 0.05f);
        }

        public static void SpawnImpact(Vector3 point, Vector3 normal)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactSpark";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point + normal * 0.02f;
            go.transform.localScale = Vector3.one * 0.07f;
            if (impactMaterial == null)
            {
                impactMaterial = new Material(Shader.Find("Standard"));
                impactMaterial.color = new Color(1f, 0.85f, 0.5f);
                impactMaterial.EnableKeyword("_EMISSION");
                impactMaterial.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.4f) * 2f);
            }
            go.GetComponent<Renderer>().material = impactMaterial;
            Object.Destroy(go, 0.08f);
        }

        private static Texture2D softTexture;
        private static Material etherParticleMaterial;

        // A soft radial-falloff sprite, so each particle is a fuzzy puff of gas
        // rather than a hard-edged sphere. Many overlapping puffs read as smoke.
        private static Texture2D SoftTexture()
        {
            if (softTexture != null) return softTexture;
            const int size = 64;
            softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c, dy = (y - c) / c;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = 1f - d;
                    a = a * a * (3f - 2f * a); // smoothstep falloff to soft edges
                    softTexture.SetPixel(x, y, new Color(a, a, a, a));
                }
            softTexture.Apply();
            return softTexture;
        }

        // Additive particle material (glowing gas) built on the soft sprite.
        private static Material EtherParticleMaterial()
        {
            if (etherParticleMaterial != null) return etherParticleMaterial;
            var sh = Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Particles/Additive")
                     ?? Shader.Find("Sprites/Default");
            etherParticleMaterial = new Material(sh) { mainTexture = SoftTexture() };
            return etherParticleMaterial;
        }

        /// <summary>Destiny-style precision-kill ether blast: a light flash plus a
        /// soft, rising, billowing cloud of glowing arc-blue gas.</summary>
        public static void SpawnEtherBurst(Vector3 pos)
        {
            var ether = new Color(0.4f, 0.8f, 1f);

            var lightGO = new GameObject("EtherFlash");
            lightGO.transform.position = pos;
            var light = lightGO.AddComponent<Light>();
            light.color = ether;
            light.range = 7f;
            light.intensity = 7f;
            lightGO.AddComponent<EtherLight>();

            var go = new GameObject("EtherBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
            main.startColor = new Color(0.55f, 0.8f, 1f, 1f);
            main.gravityModifier = -0.06f; // ether drifts upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)36) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var grow = new AnimationCurve();
            grow.AddKey(0f, 0.5f);
            grow.AddKey(0.3f, 1f);
            grow.AddKey(1f, 1.7f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, grow);

            // Additive particles fade by going dark, so the gradient ends at black.
            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.97f, 1f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0.4f),
                    new GradientColorKey(new Color(0.08f, 0.25f, 0.6f), 0.8f),
                    new GradientColorKey(Color.black, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.6f;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = EtherParticleMaterial();
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            psRenderer.sortMode = ParticleSystemSortMode.None;

            ps.Play();
        }
    }

    /// <summary>Quick flash that dims a point light to nothing, then self-destructs.</summary>
    public class EtherLight : MonoBehaviour
    {
        private const float Life = 0.25f;
        private Light flash;
        private float intensity, age;

        private void Awake()
        {
            flash = GetComponent<Light>();
            intensity = flash.intensity;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Life);
            if (flash != null) flash.intensity = intensity * (1f - t);
            if (age >= Life) Destroy(gameObject);
        }
    }
}
