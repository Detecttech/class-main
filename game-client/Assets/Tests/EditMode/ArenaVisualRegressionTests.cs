using System.Collections.Generic;
using NUnit.Framework;
using QuizBattle.Arena;
using QuizBattle.Arena.Vfx;
using QuizBattle.Arena.Visuals;
using UnityEngine;

namespace QuizBattle.Tests.EditMode
{
    public class ArenaVisualRegressionTests
    {
        private readonly List<ParticleSystem> _effects = new List<ParticleSystem>();
        private GameObject _gridObject;
        private Mesh _mesh;
        private float _fixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _fixedDeltaTime = Time.fixedDeltaTime;
            Time.fixedDeltaTime = 0.01f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ps in _effects)
            {
                if (ps == null) continue;
                var main = ps.main;
                main.stopAction = ParticleSystemStopAction.None;
                Object.DestroyImmediate(ps.gameObject);
            }
            _effects.Clear();
            if (_gridObject != null) Object.DestroyImmediate(_gridObject);
            if (_mesh != null) Object.DestroyImmediate(_mesh);
            Time.fixedDeltaTime = _fixedDeltaTime;
        }

        [TestCase("cone")]
        [TestCase("truncated-cone")]
        [TestCase("torus")]
        public void ProceduralMeshesHaveCenteredUvsAndWhiteColorsForGlow(string shape)
        {
            _mesh = shape == "torus"
                    ? PrimitiveMeshFactory.Torus(1.3f, 0.2f, 17, 9)
                    : PrimitiveMeshFactory.Cone(13, 0.7f, shape == "cone" ? 0f : 0.3f, 1.4f);

            Assert.Greater(_mesh.vertexCount, 0);
            var uv = _mesh.uv;
            var colors = _mesh.colors;
            Assert.AreEqual(_mesh.vertexCount, uv.Length);
            Assert.AreEqual(_mesh.vertexCount, colors.Length);
            for (int i = 0; i < _mesh.vertexCount; i++)
            {
                Assert.AreEqual(new Vector2(0.5f, 0.5f), uv[i], $"UV at vertex {i}");
                Assert.AreEqual(Color.white, colors[i], $"Color at vertex {i}");
            }
        }

        [Test]
        public void TorusTrianglesWindOutwardAroundTheEntireTube()
        {
            const float majorRadius = 1.3f;
            _mesh = PrimitiveMeshFactory.Torus(majorRadius, 0.2f, 17, 9);
            var vertices = _mesh.vertices;
            var normals = _mesh.normals;
            var triangles = _mesh.triangles;
            Assert.AreEqual(17 * 9 * 6, triangles.Length);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
                Vector3 tubeCenter = new Vector3(center.x, 0f, center.z).normalized * majorRadius;
                Assert.Greater(faceNormal.sqrMagnitude, 0f, $"Degenerate triangle {i / 3}");
                Assert.Greater(Vector3.Dot(faceNormal, center - tubeCenter), 0f, $"Inward triangle {i / 3}");
                Assert.Greater(Vector3.Dot(faceNormal, normals[a] + normals[b] + normals[c]), 0f,
                               $"Winding disagrees with normals at triangle {i / 3}");
            }
        }

        [TestCase(0, 0)]
        [TestCase(3, 5)]
        [TestCase(-2, 1)]
        public void TileToWorldPosTransformsTheLocalSurfaceOffsetWithoutBuildingGrid(int x, int y)
        {
            _gridObject = new GameObject("GridUnderTest");
            var grid = _gridObject.AddComponent<GridController>();
            grid.tileSize = 1.7f;
            grid.transform.position = new Vector3(4f, 7f, -3f);
            grid.transform.rotation = Quaternion.Euler(23f, 41f, -17f);
            grid.transform.localScale = new Vector3(2f, 3f, 0.5f);

            Vector3 local = new Vector3(x * grid.tileSize, 0.01f, y * grid.tileSize);
            Vector3 world = grid.TileToWorldPos(x, y);
            Assert.Less(Vector3.Distance(grid.transform.TransformPoint(local), world), 0.00001f);
            Assert.Less(Vector3.Distance(local, grid.transform.InverseTransformPoint(world)), 0.00001f);
            Assert.AreEqual(0, grid.transform.childCount);
        }

        [TestCase("vfx_fireball", 8, 0.5f, 4)]
        [TestCase("vfx_shield_shimmer", 8, 0.24f, 3)]
        [TestCase("vfx_wind_trail", 7, 0.4f, 4)]
        [TestCase("vfx_life_drain", 9, 0.5f, 2)]
        [TestCase("vfx_freeze", 10, 0.52f, 2)]
        [TestCase("unknown_ability", 5, 0.33f, 2)]
        public void AbilitiesHaveFiniteSingleBurstsAndDelayedImpactAndElimination(
            string tag, int abilityCount, float impact, int impactIndex)
        {
            _effects.AddRange(AbilityVfxPlayer.Play(tag, Vector3.zero, Vector3.right * 3.6f, true));

            Assert.AreEqual(abilityCount + 2, _effects.Count);
            Assert.AreEqual(0f, _effects[0].main.startDelay.constant);
            Assert.That(_effects[impactIndex].main.startDelay.constant, Is.EqualTo(impact).Within(0.00001f));
            for (int i = abilityCount; i < _effects.Count; i++)
                Assert.That(_effects[i].main.startDelay.constant, Is.EqualTo(impact + 0.06f).Within(0.00001f));

            int totalCapacity = 0;
            foreach (var ps in _effects)
            {
                var main = ps.main;
                Assert.IsFalse(main.loop, ps.name);
                Assert.IsFalse(main.playOnAwake, ps.name);
                Assert.AreEqual(ParticleSystemStopAction.Destroy, main.stopAction, ps.name);
                Assert.That(main.duration, Is.InRange(0.02f, 0.8f), ps.name);
                Assert.AreEqual(ParticleSystemCurveMode.Constant, main.startLifetime.mode, ps.name);
                Assert.That(main.startLifetime.constant, Is.EqualTo(main.duration).Within(0.00001f), ps.name);
                Assert.AreEqual(ParticleSystemCurveMode.Constant, main.startDelay.mode, ps.name);
                Assert.That(main.startDelay.constant, Is.InRange(0f, 1.1f), ps.name);
                Assert.LessOrEqual(main.startDelay.constant + main.duration, 1.6f, ps.name);
                Assert.That(main.maxParticles, Is.InRange(1, 48), ps.name);
                Assert.IsFalse(ps.useAutoRandomSeed, ps.name);
                Assert.AreEqual(1729u, ps.randomSeed, ps.name);

                var emission = ps.emission;
                Assert.IsTrue(emission.enabled, ps.name);
                Assert.AreEqual(0f, emission.rateOverTime.constant, ps.name);
                Assert.AreEqual(0f, emission.rateOverDistance.constant, ps.name);
                Assert.AreEqual(1, emission.burstCount, ps.name);
                var burst = emission.GetBurst(0);
                Assert.AreEqual(0f, burst.time, ps.name);
                Assert.AreEqual(1, burst.cycleCount, ps.name);
                Assert.AreEqual(1f, burst.probability, ps.name);
                Assert.AreEqual(ParticleSystemCurveMode.Constant, burst.count.mode, ps.name);
                Assert.AreEqual((float)main.maxParticles, burst.count.constant, ps.name);
                totalCapacity += main.maxParticles;
            }
            Assert.LessOrEqual(totalCapacity, 64);

            AbilityVfxPlayer.SimulateAll(_effects, 2f);
            foreach (var ps in _effects)
            {
                Assert.IsNotNull(ps);
                Assert.AreEqual(0, ps.particleCount, ps.name);
                Assert.IsFalse(ps.IsAlive(false), ps.name);
                Assert.AreEqual(ParticleSystemStopAction.Destroy, ps.main.stopAction, ps.name);
            }
        }

        [TestCase(-1f, -5, -1f, 0.02f, 1, 0f)]
        [TestCase(0f, 1000, 0.25f, 0.02f, 48, 0.25f)]
        [TestCase(0.35f, 14, 0.1f, 0.35f, 14, 0.1f)]
        public void ParticleFactoryBoundsDurationCountAndDelay(
            float duration, int count, float delay, float expectedDuration, int expectedCount, float expectedDelay)
        {
            var effect = ParticleFactory.Burst(Vector3.zero, Color.white, count: count, lifetime: duration);
            var ps = effect.GetComponent<ParticleSystem>();
            _effects.Add(ps);
            ParticleFactory.Delay(effect, delay);

            var main = ps.main;
            Assert.That(main.duration, Is.EqualTo(expectedDuration).Within(0.00001f));
            Assert.That(main.startLifetime.constant, Is.EqualTo(expectedDuration).Within(0.00001f));
            Assert.That(main.startDelay.constant, Is.EqualTo(expectedDelay).Within(0.00001f));
            Assert.AreEqual(expectedCount, main.maxParticles);
            Assert.AreEqual((float)expectedCount, ps.emission.GetBurst(0).count.constant);
            Assert.IsFalse(main.loop);
        }

        [Test]
        public void SimulateAllReplaysFireballBeforeImpactAfterImpactAndAfterEnd()
        {
            _effects.AddRange(AbilityVfxPlayer.Play("vfx_fireball", Vector3.zero, Vector3.right * 3.6f, false));
            Assert.AreEqual(8, _effects.Count);

            foreach (float time in new[] { 0.3f, 0.58f })
            {
                AbilityVfxPlayer.SimulateAll(_effects, time);
                var snapshots = new ParticleSystem.Particle[_effects.Count][];
                for (int i = 0; i < _effects.Count; i++)
                {
                    var ps = _effects[i];
                    bool active = time < 0.5f ? i == 2 || i == 3 : i >= 4;
                    Assert.AreEqual(active ? ps.main.maxParticles : 0, ps.particleCount, $"{ps.name} at {time}");
                    snapshots[i] = new ParticleSystem.Particle[ps.particleCount];
                    Assert.AreEqual(snapshots[i].Length, ps.GetParticles(snapshots[i]));
                }

                AbilityVfxPlayer.SimulateAll(_effects, 2f);
                foreach (var ps in _effects)
                {
                    Assert.AreEqual(0, ps.particleCount, ps.name);
                    Assert.IsFalse(ps.IsAlive(false), ps.name);
                }

                AbilityVfxPlayer.SimulateAll(_effects, time);
                for (int i = 0; i < _effects.Count; i++)
                {
                    var ps = _effects[i];
                    var replay = new ParticleSystem.Particle[ps.main.maxParticles];
                    Assert.AreEqual(snapshots[i].Length, ps.GetParticles(replay), ps.name);
                    Assert.AreEqual(ParticleSystemStopAction.Destroy, ps.main.stopAction, ps.name);
                    for (int j = 0; j < snapshots[i].Length; j++)
                    {
                        Assert.AreEqual(snapshots[i][j].randomSeed, replay[j].randomSeed, ps.name);
                        Assert.Less(Vector3.Distance(snapshots[i][j].position, replay[j].position), 0.00001f, ps.name);
                        Assert.Less(Vector3.Distance(snapshots[i][j].velocity, replay[j].velocity), 0.00001f, ps.name);
                        Assert.That(replay[j].remainingLifetime,
                                    Is.EqualTo(snapshots[i][j].remainingLifetime).Within(0.00001f), ps.name);
                    }
                }
            }
        }
    }
}
