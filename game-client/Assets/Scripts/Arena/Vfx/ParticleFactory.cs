using QuizBattle.Arena.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuizBattle.Arena.Vfx
{
    public static class ParticleFactory
    {
        public enum Form { Orb, Crescent, Crystal, Shield, Slash, Ring, Beam }

        private static readonly Mesh[] Meshes = new Mesh[7];

        public static GameObject Burst(Vector3 position, Color color, float size = 0.15f, int count = 14, float speed = 2f, float lifetime = 0.35f)
        {
            var ps = Create("Burst", position, color, lifetime, count);
            var main = ps.main;
            main.startSize = size;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            SetSize(ps, 1f, 0f);
            return Finish(ps);
        }

        public static GameObject Streaks(Vector3 from, Vector3 to, Color color, float duration = 0.3f, int count = 8)
        {
            var ps = Create("Streaks", from, color, duration, count);
            var main = ps.main;
            main.startSize = 0.08f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Min(0.1f, Vector3.Distance(from, to) * 0.05f);
            SetMotion(ps, to - from, duration, 0f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.5f;
            renderer.velocityScale = 0.025f;
            return Finish(ps);
        }

        public static GameObject RingWave(Vector3 position, Color color, float startRadius = 0.15f, float endRadius = 0.6f, float duration = 0.35f, bool upright = true)
        {
            var ps = Create("RingWave", position, color, duration, 1);
            SetMesh(ps, Form.Ring, Vector3.one, upright ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity);
            SetSize(ps, startRadius, endRadius);
            return Finish(ps);
        }

        public static GameObject Beam(Vector3 from, Vector3 to, Color color, float duration = 0.4f, float width = 0.045f)
        {
            Vector3 delta = to - from;
            return MeshPulse((from + to) * 0.5f, color, Form.Beam,
                             new Vector3(width, width, Mathf.Max(delta.magnitude, 0.01f)), duration,
                             delta.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(delta) : Quaternion.identity);
        }

        public static GameObject MeshPulse(Vector3 position, Color color, Form form, Vector3 scale, float duration, Quaternion rotation = default)
        {
            var ps = Create(form.ToString(), position, color, duration, 1);
            SetMesh(ps, form, scale, rotation.Equals(default(Quaternion)) ? Quaternion.identity : rotation);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                        new Keyframe(0f, 0.2f), new Keyframe(0.18f, 1f),
                        new Keyframe(0.65f, 1.05f), new Keyframe(1f, 0.85f)));
            if (form == Form.Slash || form == Form.Crescent)
            {
                var spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = 4f;
            }
            return Finish(ps);
        }

        public static GameObject Projectile(Vector3 from, Vector3 to, Color color, Form form, Vector3 scale, float duration, float arcHeight = 0f, float roll = 0f)
        {
            var ps = Create("Projectile_" + form, from, color, duration, 1);
            Vector3 delta = to - from;
            Quaternion facing = delta.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(delta) : Quaternion.identity;
            if ((form == Form.Crescent || form == Form.Slash) && Camera.main != null)
                facing = Camera.main.transform.rotation;
            SetMesh(ps, form, scale, facing * Quaternion.Euler(0f, 0f, roll));
            SetMotion(ps, delta, duration, arcHeight);
            SetSize(ps, 0.85f, 1f);
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = false;
            if (form == Form.Crescent || form == Form.Slash)
            {
                var spin = ps.rotationOverLifetime;
                spin.enabled = true;
                spin.z = form == Form.Crescent ? 8f : -5f;
            }
            if (form == Form.Orb || form == Form.Crystal)
            {
                var trails = ps.trails;
                trails.enabled = true;
                trails.ratio = 1f;
                trails.lifetime = 0.13f / Mathf.Max(0.02f, duration);
                trails.sizeAffectsLifetime = false;
                trails.minVertexDistance = Mathf.Max(0.08f, (delta.magnitude + Mathf.Abs(arcHeight) * 2f) / 12f);
                trails.worldSpace = true;
                trails.dieWithParticles = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.65f, 1f, 0f));
                ps.GetComponent<ParticleSystemRenderer>().trailMaterial = ToonMaterialFactory.Glow(Color.white, 1.25f, 0.5f);
            }
            return Finish(ps);
        }

        public static GameObject Delay(GameObject go, float delay)
        {
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.None;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main.startDelay = Mathf.Max(0f, delay);
            return Finish(ps);
        }

        private static ParticleSystem Create(string name, Vector3 position, Color color, float duration, int count)
        {
            var go = new GameObject("Vfx_" + name);
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.useAutoRandomSeed = false;
            ps.randomSeed = 1729;
            duration = Mathf.Max(0.02f, duration);
            count = Mathf.Clamp(count, 1, 48);
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = duration;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.enabled = false;
            var gradient = new Gradient();
            gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = ToonMaterialFactory.Glow(Color.white, 1.6f, 0.45f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.maxParticleSize = 1f;
            return ps;
        }

        private static GameObject Finish(ParticleSystem ps)
        {
            var main = ps.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ps.Play();
            return ps.gameObject;
        }

        private static void SetSize(ParticleSystem ps, float start, float end)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
        }

        private static void SetMotion(ParticleSystem ps, Vector3 delta, float duration, float arcHeight)
        {
            duration = Mathf.Max(0.02f, duration);
            Vector3 velocity = delta / duration;
            var motion = ps.velocityOverLifetime;
            motion.enabled = true;
            motion.space = ParticleSystemSimulationSpace.World;
            motion.x = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, velocity.x, 1f, velocity.x));
            motion.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, velocity.y + 4f * arcHeight / duration, 1f, velocity.y - 4f * arcHeight / duration));
            motion.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, velocity.z, 1f, velocity.z));
        }

        private static void SetMesh(ParticleSystem ps, Form form, Vector3 scale, Quaternion rotation)
        {
            var main = ps.main;
            main.startSize3D = true;
            main.startSizeX = scale.x;
            main.startSizeY = scale.y;
            main.startSizeZ = scale.z;
            main.startRotation3D = true;
            Vector3 angles = rotation.eulerAngles * Mathf.Deg2Rad;
            main.startRotationX = angles.x;
            main.startRotationY = angles.y;
            main.startRotationZ = angles.z;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.mesh = GetMesh(form);
        }

        private static Mesh GetMesh(Form form)
        {
            int index = (int)form;
            if (Meshes[index] != null) return Meshes[index];
            Vector3[] vertices;
            int[] triangles;
            if (form == Form.Ring || form == Form.Crescent || form == Form.Slash)
            {
                const int segments = 24;
                vertices = new Vector3[(segments + 1) * 2];
                triangles = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float angle = form == Form.Ring ? t * Mathf.PI * 2f : Mathf.Lerp(-1.3f, 1.3f, t);
                    float radius = form == Form.Ring ? 1f : 0.7f;
                    float width = form == Form.Ring ? 0.055f : Mathf.Sin(t * Mathf.PI) * (form == Form.Slash ? 0.22f : 0.12f);
                    Vector3 direction = form == Form.Ring
                                        ? new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle))
                                        : new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    vertices[i * 2] = direction * (radius - width);
                    vertices[i * 2 + 1] = direction * (radius + width);
                    if (i == segments) continue;
                    int v = i * 2;
                    int tri = i * 6;
                    triangles[tri] = v; triangles[tri + 1] = v + 2; triangles[tri + 2] = v + 1;
                    triangles[tri + 3] = v + 1; triangles[tri + 4] = v + 2; triangles[tri + 5] = v + 3;
                }
            }
            else if (form == Form.Orb)
            {
                const int segments = 10;
                const int rows = 6;
                vertices = new Vector3[(rows + 1) * segments];
                triangles = new int[rows * segments * 6];
                for (int row = 0; row <= rows; row++)
                {
                    float latitude = row * Mathf.PI / rows;
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        int v = row * segments + i;
                        vertices[v] = new Vector3(Mathf.Cos(angle) * Mathf.Sin(latitude), Mathf.Cos(latitude), Mathf.Sin(angle) * Mathf.Sin(latitude)) * 0.5f;
                        if (row == rows) continue;
                        int next = row * segments + (i + 1) % segments;
                        int tri = v * 6;
                        triangles[tri] = v; triangles[tri + 1] = next; triangles[tri + 2] = v + segments;
                        triangles[tri + 3] = next; triangles[tri + 4] = next + segments; triangles[tri + 5] = v + segments;
                    }
                }
            }
            else if (form == Form.Shield)
            {
                const int segments = 12;
                vertices = new Vector3[segments * 3 + 1];
                triangles = new int[segments * 15];
                for (int row = 0; row < 3; row++)
                {
                    float latitude = row * Mathf.PI / 6f;
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        vertices[row * segments + i] = new Vector3(Mathf.Cos(angle) * Mathf.Cos(latitude), Mathf.Sin(latitude), Mathf.Sin(angle) * Mathf.Cos(latitude));
                        int next = (i + 1) % segments;
                        int a = row * segments + i;
                        int b = row * segments + next;
                        int tri = row < 2 ? (row * segments + i) * 6 : segments * 12 + i * 3;
                        triangles[tri] = a; triangles[tri + 1] = b;
                        triangles[tri + 2] = row < 2 ? a + segments : segments * 3;
                        if (row == 2) continue;
                        triangles[tri + 3] = b; triangles[tri + 4] = b + segments; triangles[tri + 5] = a + segments;
                    }
                }
                vertices[segments * 3] = Vector3.up;
            }
            else
            {
                float tip = form == Form.Crystal ? 1f : 0.5f;
                vertices = new[] { new Vector3(0f, 0f, tip), new Vector3(0f, 0f, -tip), Vector3.right * 0.5f, Vector3.up * 0.5f, Vector3.left * 0.5f, Vector3.down * 0.5f };
                triangles = new[] { 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 2, 1, 3, 2, 1, 4, 3, 1, 5, 4, 1, 2, 5 };
            }
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uv[i] = new Vector2(0.5f, 0.5f);
                colors[i] = Color.white;
            }
            var mesh = new Mesh { name = "VfxMesh_" + form, hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Meshes[index] = mesh;
            return mesh;
        }
    }
}
