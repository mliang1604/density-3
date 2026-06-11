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

        private static Material boltMaterial;
        private static readonly Material[] elementBoltMaterials = new Material[3];

        /// <summary>Glowing purple energy-bolt body: visuals only, no collider —
        /// the caller adds flight behavior.</summary>
        public static GameObject SpawnBolt(Vector3 position)
        {
            if (boltMaterial == null)
                boltMaterial = MakeEmissiveMaterial(new Color(0.6f, 0.2f, 1f), new Color(0.55f, 0.2f, 1f) * 2.5f);
            return SpawnBolt(position, "EnemyBolt", boltMaterial);
        }

        /// <summary>Element-tinted bolt body for ability projectiles.</summary>
        public static GameObject SpawnBolt(Vector3 position, Element element)
        {
            int i = (int)element;
            if (elementBoltMaterials[i] == null)
                elementBoltMaterials[i] = MakeEmissiveMaterial(
                    ElementPalette.Base(element), ElementPalette.Emission(element));
            return SpawnBolt(position, "ElementBolt", elementBoltMaterials[i]);
        }

        private static GameObject SpawnBolt(Vector3 position, string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.22f;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private static Material MakeEmissiveMaterial(Color color, Color emission)
        {
            var m = new Material(Shader.Find("Standard")) { color = color };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            return m;
        }

        private static readonly Material[] energyCoreMaterials = new Material[3];

        /// <summary>
        /// Churning element-colored energy sphere (vortex grenades, nova
        /// vortexes): a dark translucent core silhouette wrapped in a swirling
        /// additive particle shell emitted at exactly the given radius, plus a
        /// glow light — the visual edge IS the damage edge. Caller owns the
        /// lifetime: parent the returned object to the zone.
        /// </summary>
        public static GameObject SpawnEnergySphere(Vector3 center, Element element, float radius)
        {
            var root = new GameObject("EnergySphere");
            root.transform.position = center;

            int ei = (int)element;
            if (energyCoreMaterials[ei] == null)
            {
                // Sprites/Default (already pinned for WebGL) renders an unlit
                // translucent silhouette — the void-dark heart of the sphere.
                energyCoreMaterials[ei] = new Material(Shader.Find("Sprites/Default"));
                Color deep = ElementPalette.Base(element) * 0.25f;
                deep.a = 0.85f;
                energyCoreMaterials[ei].color = deep;
            }

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * (radius * 1.7f);
            var coreRenderer = core.GetComponent<Renderer>();
            coreRenderer.material = energyCoreMaterials[ei];
            coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;

            Color c = ElementPalette.Base(element);

            // Shell particles cling to the sphere surface and swirl; viewed
            // from any angle they stack up at the silhouette, which brightens
            // the rim exactly like the reference.
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.10f, radius * 0.18f);
            main.startColor = Color.Lerp(c, Color.white, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 220;

            var emission = ps.emission;
            emission.rateOverTime = 70f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            shape.radiusThickness = 0f; // surface only — the shell is the edge

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(2.5f); // the churn

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(c, Color.white, 0.6f), 0f),
                    new GradientColorKey(c, 0.5f),
                    new GradientColorKey(Color.black, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var psRenderer = root.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = EtherParticleMaterial();
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            psRenderer.sortMode = ParticleSystemSortMode.None;

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = c;
            light.range = radius * 3f;
            light.intensity = 3f;

            return root;
        }

        /// <summary>Looping element-colored ember emitter attached to a host
        /// (burning supers, lit weapons): soft additive sparks drifting upward
        /// in local space, so they ride the host's motion. Lifetime is the
        /// host's; position the returned system's transform to taste.</summary>
        public static ParticleSystem AttachEmbers(GameObject host, Element element,
            float rate = 14f, float emberSize = 0.022f)
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(host.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            Color c = ElementPalette.Base(element);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(emberSize * 0.7f, emberSize * 1.3f);
            main.startColor = Color.Lerp(c, Color.white, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.05f, 0.04f, 0.38f); // a barrel's length

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); // embers rise

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(c, Color.white, 0.5f), 0f),
                    new GradientColorKey(c, 0.5f),
                    new GradientColorKey(Color.black, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = EtherParticleMaterial();
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;
            psRenderer.sortMode = ParticleSystemSortMode.None;

            return ps;
        }

        /// <summary>Element-colored energy trail for ability projectiles —
        /// turns a bare bolt into a streak of its element.</summary>
        public static TrailRenderer AddElementTrail(GameObject host, Element element, float width = 0.3f)
        {
            var tr = host.AddComponent<TrailRenderer>();
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            tr.material = lineMaterial;
            tr.time = 0.25f;
            tr.startWidth = width;
            tr.endWidth = 0f;
            Color c = ElementPalette.Base(element);
            tr.startColor = c;
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            return tr;
        }

        /// <summary>Flat ground-level circle marking an ability zone's radius
        /// (rifts). Caller-owned — parent it to the zone so they expire
        /// together.</summary>
        public static LineRenderer SpawnGroundRing(Vector3 center, float radius, Element element,
            float width = 0.08f)
        {
            const int segments = 40;
            var go = new GameObject("GroundRing");
            var lr = go.AddComponent<LineRenderer>();
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.material = lineMaterial;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startWidth = width;
            lr.endWidth = width;
            Color c = ElementPalette.Base(element);
            lr.startColor = c;
            lr.endColor = c;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            for (int i = 0; i < segments; i++)
            {
                float a = (float)i / segments * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0.03f, Mathf.Sin(a) * radius));
            }
            return lr;
        }

        /// <summary>Element-tinted beam (tripmine lasers, sniper telegraphs).
        /// Returns the LineRenderer so callers can move it; seconds &lt;= 0 makes
        /// it persistent and caller-owned.</summary>
        public static LineRenderer SpawnBeam(Vector3 start, Vector3 end, Element element,
            float width = 0.04f, float seconds = 0f)
        {
            var go = new GameObject("Beam");
            var lr = go.AddComponent<LineRenderer>();
            if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.material = lineMaterial;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = width;
            lr.endWidth = width;
            Color c = ElementPalette.Base(element);
            c.a = 0.9f;
            lr.startColor = c;
            lr.endColor = c;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (seconds > 0f) Object.Destroy(go, seconds);
            return lr;
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
            SpawnBurst(pos, new Color(0.4f, 0.8f, 1f), new Color(0.55f, 0.8f, 1f, 1f), new[]
            {
                new GradientColorKey(new Color(0.85f, 0.97f, 1f), 0f),
                new GradientColorKey(new Color(0.4f, 0.8f, 1f), 0.4f),
                new GradientColorKey(new Color(0.08f, 0.25f, 0.6f), 0.8f),
                new GradientColorKey(Color.black, 1f)
            }, 1f, 36);
        }

        /// <summary>Element-tinted ability burst (grenade detonations, melee and
        /// super payoffs). scale grows the cloud and flash for bigger hits.</summary>
        public static void SpawnElementBurst(Vector3 pos, Element element, float scale = 1f)
        {
            Color c = ElementPalette.Base(element);
            SpawnBurst(pos, c, Color.Lerp(c, Color.white, 0.35f), new[]
            {
                new GradientColorKey(Color.Lerp(c, Color.white, 0.65f), 0f),
                new GradientColorKey(c, 0.4f),
                new GradientColorKey(c * 0.3f, 0.8f),
                new GradientColorKey(Color.black, 1f)
            }, scale, Mathf.RoundToInt(30f * Mathf.Max(1f, scale)));
        }

        /// <summary>Shared flash-plus-gas-cloud burst. Color identity comes in via
        /// the light color, particle start color, and the over-lifetime gradient
        /// (additive particles fade by going dark, so gradients end at black).</summary>
        private static void SpawnBurst(Vector3 pos, Color lightColor, Color startColor,
            GradientColorKey[] colorKeys, float scale, int particles)
        {
            var lightGO = new GameObject("BurstFlash");
            lightGO.transform.position = pos;
            var light = lightGO.AddComponent<Light>();
            light.color = lightColor;
            light.range = 7f * scale;
            light.intensity = 7f;
            lightGO.AddComponent<EtherLight>();

            var go = new GameObject("Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 2.2f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f * scale, 0.45f * scale);
            main.startColor = startColor;
            main.gravityModifier = -0.06f; // the gas drifts upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(80, particles * 2);
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particles) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f * scale;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            var grow = new AnimationCurve();
            grow.AddKey(0f, 0.5f);
            grow.AddKey(0.3f, 1f);
            grow.AddKey(1f, 1.7f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, grow);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(colorKeys,
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
