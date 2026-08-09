using QuizBattle.Arena.Visuals;
using UnityEngine;

namespace QuizBattle.Arena.Vfx
{
    /// Code-only ParticleSystem builders — no external VFX assets. Each function returns
    /// a self-destroying GameObject; callers don't need to track or clean these up.
    /// Animation (fade/shrink/expand) is driven entirely by ParticleSystem's built-in
    /// curve modules rather than a custom Update() loop, keeping this a data-only factory.
    public static class ParticleFactory
    {
        public static GameObject Burst(Vector3 position, Color color, float size = 0.15f, int count = 14, float speed = 2f, float lifetime = 0.35f)
        {
            var go = new GameObject("Vfx_Burst");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = lifetime;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var sizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = colorGradient;

            ApplyGlowRenderer(ps, color);
            ps.Play();

            Object.Destroy(go, lifetime + 0.2f);
            return go;
        }

        public static GameObject Streaks(Vector3 from, Vector3 to, Color color, float duration = 0.3f, int count = 8)
        {
            var go = new GameObject("Vfx_Streaks");
            go.transform.position = from;

            var ps = go.AddComponent<ParticleSystem>();
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            Vector3 velocity = duration > 0.001f ? delta / duration : Vector3.zero;

            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = duration;
            main.startSpeed = 0f;
            main.startSize = 0.08f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.12f, distance * 0.08f);

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = velocity.x;
            vol.y = velocity.y;
            vol.z = velocity.z;

            var col = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.enabled = true;
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 4f;
            renderer.sharedMaterial = ToonMaterialFactory.GlowInstance(color, intensity: 1.9f, softEdge: 0.6f);

            ps.Play();
            Object.Destroy(go, duration + 0.2f);
            return go;
        }

        /// A single expanding ring using the shared Torus mesh, animated purely via the
        /// size-over-lifetime curve (no custom Update loop).
        public static GameObject RingWave(Vector3 position, Color color, float startRadius = 0.15f, float endRadius = 0.6f, float duration = 0.35f, bool upright = true)
        {
            var go = new GameObject("Vfx_RingWave");
            go.transform.position = position;
            go.transform.rotation = upright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = duration;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape;
            shape.enabled = false;

            var sizeCurve = new AnimationCurve(new Keyframe(0f, startRadius), new Keyframe(1f, endRadius));
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            col.enabled = true;
            col.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = PrimitiveMeshFactory.Torus(0.5f, 0.05f, 20, 6);
            renderer.sharedMaterial = ToonMaterialFactory.GlowInstance(color, intensity: 2.1f, softEdge: 0.5f);

            ps.Play();
            Object.Destroy(go, duration + 0.2f);
            return go;
        }

        /// A static pulsing beam between two points — not particle-based, just a thin
        /// scaled box with the glow shader's built-in pulse (cheaper and simpler than
        /// animating a LineRenderer for a short-lived effect).
        public static GameObject Beam(Vector3 from, Vector3 to, Color color, float duration = 0.4f, float width = 0.045f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Vfx_Beam";
            Object.Destroy(go.GetComponent<Collider>());

            Vector3 mid = (from + to) * 0.5f;
            float length = Vector3.Distance(from, to);
            go.transform.position = mid;
            go.transform.rotation = length > 0.001f ? Quaternion.LookRotation((to - from).normalized) : Quaternion.identity;
            go.transform.localScale = new Vector3(width, width, Mathf.Max(length, 0.01f));

            go.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.GlowInstance(color, intensity: 2.6f, softEdge: 0.7f);
            // Manual pulse since GlowInstance defaults to no pulse; set it directly here
            // rather than adding pulse parameters to every GlowInstance call site.
            go.GetComponent<Renderer>().sharedMaterial.SetFloat("_PulseSpeed", 10f);
            go.GetComponent<Renderer>().sharedMaterial.SetFloat("_PulseAmount", 0.4f);

            Object.Destroy(go, duration);
            return go;
        }

        private static void ApplyGlowRenderer(ParticleSystem ps, Color color)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ToonMaterialFactory.GlowInstance(color, intensity: 2.3f, softEdge: 0.4f);
        }
    }
}
