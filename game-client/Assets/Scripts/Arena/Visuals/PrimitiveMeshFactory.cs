using System.Collections.Generic;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    /// Procedural meshes for shapes GameObject.CreatePrimitive doesn't offer (cone,
    /// torus). Cached by parameter key, same stale-reference guard as ToonMaterialFactory
    /// since these are also plain runtime assets that can be reclaimed on a scene unload.
    /// Cones use flat (hard, per-face) normals — faceted low-poly reads better under the
    /// toon shader's banded lighting than a smoothed surface. Tori use smooth analytic
    /// normals since they represent rings/halos, not faceted silhouette pieces.
    public static class PrimitiveMeshFactory
    {
        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();

        /// A cone (or truncated cone when radiusTop > 0) with its base centered at the
        /// local origin (y=0) and apex/top circle at y=height.
        public static Mesh Cone(int segments, float radiusBottom, float radiusTop, float height)
        {
            string key = $"cone|{segments}|{radiusBottom}|{radiusTop}|{height}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float angleStep = 2f * Mathf.PI / segments;

            for (int i = 0; i < segments; i++)
            {
                float a0 = i * angleStep;
                float a1 = (i + 1) * angleStep;

                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radiusBottom, 0f, Mathf.Sin(a0) * radiusBottom);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radiusBottom, 0f, Mathf.Sin(a1) * radiusBottom);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * radiusTop, height, Mathf.Sin(a0) * radiusTop);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * radiusTop, height, Mathf.Sin(a1) * radiusTop);

                Vector3 faceNormal = Vector3.Cross(t0 - b0, b1 - b0).normalized;
                if (faceNormal.sqrMagnitude < 0.0001f) faceNormal = Vector3.Cross(t1 - b0, b1 - b0).normalized;

                int start = vertices.Count;
                vertices.Add(b0); vertices.Add(b1); vertices.Add(t0); vertices.Add(t1);
                normals.Add(faceNormal); normals.Add(faceNormal); normals.Add(faceNormal); normals.Add(faceNormal);

                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start + 3);
            }

            if (radiusBottom > 0f) AddCap(vertices, normals, triangles, radiusBottom, 0f, segments, angleStep, Vector3.down, capOnTop: false);
            if (radiusTop > 0f) AddCap(vertices, normals, triangles, radiusTop, height, segments, angleStep, Vector3.up, capOnTop: true);

            var mesh = BuildMesh($"QB_Cone_{segments}", vertices, normals, triangles);
            _cache[key] = mesh;
            return mesh;
        }

        /// A torus centered at the local origin, lying in the XZ plane.
        public static Mesh Torus(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
        {
            string key = $"torus|{majorRadius}|{minorRadius}|{majorSegments}|{minorSegments}";
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            int ringSize = minorSegments + 1;

            for (int i = 0; i <= majorSegments; i++)
            {
                float u = (float)i / majorSegments * 2f * Mathf.PI;
                Vector3 radialOut = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                Vector3 center = radialOut * majorRadius;

                for (int j = 0; j <= minorSegments; j++)
                {
                    float v = (float)j / minorSegments * 2f * Mathf.PI;
                    Vector3 normal = radialOut * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    vertices.Add(center + normal * minorRadius);
                    normals.Add(normal);
                }
            }

            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * ringSize + j;
                    int b = (i + 1) * ringSize + j;
                    int c = (i + 1) * ringSize + j + 1;
                    int d = i * ringSize + j + 1;

                    triangles.Add(a); triangles.Add(b); triangles.Add(d);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            var mesh = BuildMesh($"QB_Torus_{majorSegments}_{minorSegments}", vertices, normals, triangles);
            _cache[key] = mesh;
            return mesh;
        }

        private static void AddCap(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            float radius, float y, int segments, float angleStep, Vector3 normal, bool capOnTop)
        {
            int centerIdx = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));
            normals.Add(normal);

            int ringStart = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = (i % segments) * angleStep;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                normals.Add(normal);
            }

            for (int i = 0; i < segments; i++)
            {
                triangles.Add(centerIdx);
                if (capOnTop)
                {
                    triangles.Add(ringStart + i + 1);
                    triangles.Add(ringStart + i);
                }
                else
                {
                    triangles.Add(ringStart + i);
                    triangles.Add(ringStart + i + 1);
                }
            }
        }

        private static Mesh BuildMesh(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontUnloadUnusedAsset };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
