using System.Collections.Generic;
using QuizBattle.Characters;
using UnityEngine;

namespace QuizBattle.Arena.Visuals
{
    public struct CharacterVisualResult
    {
        public GameObject Root;
        public Renderer[] Renderers;
    }

    /// Builds a distinct multi-primitive silhouette per CharacterArchetype instead of the
    /// single generic capsule. Shared rules across every archetype: origin at the feet
    /// (y=0 is the ground contact point, matching GridController.TileToWorldPos), total
    /// height stays under ~1.3 tile-units so tokens read clearly on an 8-wide board, and
    /// every character gets a thin emissive ground disc in its own color — a cheap
    /// silhouette-independent position/team readability cue.
    public static class CharacterVisualBuilder
    {
        public static CharacterVisualResult Build(in CharacterVisual visual, Transform parent)
        {
            var renderers = new List<Renderer>();
            var animator = parent.gameObject.AddComponent<TokenIdleAnimator>();

            switch (visual.Archetype)
            {
                case CharacterArchetype.Fire:
                    BuildFire(parent, visual, renderers, animator);
                    break;
                case CharacterArchetype.Tank:
                    BuildTank(parent, visual, renderers);
                    break;
                case CharacterArchetype.Wind:
                    BuildWind(parent, visual, renderers, animator);
                    break;
                case CharacterArchetype.Arcane:
                    BuildArcane(parent, visual, renderers, animator);
                    break;
                default:
                    BuildGeneric(parent, visual, renderers);
                    break;
            }

            return new CharacterVisualResult { Root = parent.gameObject, Renderers = renderers.ToArray() };
        }

        private static void BuildGeneric(Transform parent, in CharacterVisual visual, List<Renderer> renderers)
        {
            var bodyMat = ToonMaterialFactory.Instance(visual.BaseColor);
            CreatePrimitivePart(parent, "Body", PrimitiveType.Capsule, new Vector3(0, 0.5f, 0), Quaternion.identity,
                new Vector3(0.6f, 0.5f, 0.6f), bodyMat, renderers);
            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void BuildFire(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var bodyMat = ToonMaterialFactory.Instance(visual.BaseColor);
            var emberMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 2.5f, softEdge: 0.3f);

            var torsoMesh = PrimitiveMeshFactory.Cone(8, 0.26f, 0.15f, 0.55f);
            CreatePart(parent, "Torso", torsoMesh, Vector3.zero, Quaternion.identity, Vector3.one, bodyMat, renderers);

            CreatePrimitivePart(parent, "Head", PrimitiveType.Sphere, new Vector3(0, 0.68f, 0), Quaternion.identity,
                Vector3.one * 0.26f, bodyMat, renderers);

            var flameMesh = PrimitiveMeshFactory.Cone(5, 0.05f, 0f, 0.18f);
            CreatePart(parent, "FlameCrestCenter", flameMesh, new Vector3(0f, 0.74f, -0.05f), Quaternion.Euler(-16f, 0, 0), Vector3.one, emberMat, renderers);
            CreatePart(parent, "FlameCrestLeft", flameMesh, new Vector3(-0.07f, 0.72f, -0.03f), Quaternion.Euler(-8f, 0, 16f), Vector3.one * 0.8f, emberMat, renderers);
            CreatePart(parent, "FlameCrestRight", flameMesh, new Vector3(0.07f, 0.72f, -0.03f), Quaternion.Euler(-8f, 0, -16f), Vector3.one * 0.8f, emberMat, renderers);

            var emberLeft = CreatePrimitivePart(parent, "EmberLeft", PrimitiveType.Sphere, new Vector3(-0.24f, 0.4f, 0.1f), Quaternion.identity, Vector3.one * 0.06f, emberMat, renderers);
            var emberRight = CreatePrimitivePart(parent, "EmberRight", PrimitiveType.Sphere, new Vector3(0.24f, 0.35f, -0.08f), Quaternion.identity, Vector3.one * 0.06f, emberMat, renderers);
            animator.Register(emberLeft.transform, bobSpeed: 1.6f, bobAmount: 0.025f);
            animator.Register(emberRight.transform, bobSpeed: 1.3f, bobAmount: 0.02f);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void BuildTank(Transform parent, in CharacterVisual visual, List<Renderer> renderers)
        {
            var bodyMat = ToonMaterialFactory.Instance(visual.BaseColor);
            var accentMat = ToonMaterialFactory.Instance(visual.AccentColor);

            CreatePrimitivePart(parent, "LegLeft", PrimitiveType.Cube, new Vector3(-0.13f, 0.12f, 0), Quaternion.identity, new Vector3(0.14f, 0.24f, 0.16f), bodyMat, renderers);
            CreatePrimitivePart(parent, "LegRight", PrimitiveType.Cube, new Vector3(0.13f, 0.12f, 0), Quaternion.identity, new Vector3(0.14f, 0.24f, 0.16f), bodyMat, renderers);

            CreatePrimitivePart(parent, "Torso", PrimitiveType.Cube, new Vector3(0, 0.45f, 0), Quaternion.identity, new Vector3(0.5f, 0.42f, 0.32f), bodyMat, renderers);
            CreatePrimitivePart(parent, "ChestPlate", PrimitiveType.Cube, new Vector3(0, 0.5f, 0.15f), Quaternion.identity, new Vector3(0.36f, 0.28f, 0.06f), accentMat, renderers);

            CreatePrimitivePart(parent, "ShoulderLeft", PrimitiveType.Cube, new Vector3(-0.3f, 0.62f, 0), Quaternion.Euler(0, 0, 16f), new Vector3(0.18f, 0.14f, 0.24f), accentMat, renderers);
            CreatePrimitivePart(parent, "ShoulderRight", PrimitiveType.Cube, new Vector3(0.3f, 0.62f, 0), Quaternion.Euler(0, 0, -16f), new Vector3(0.18f, 0.14f, 0.24f), accentMat, renderers);

            CreatePrimitivePart(parent, "Head", PrimitiveType.Sphere, new Vector3(0, 0.78f, 0), Quaternion.identity, Vector3.one * 0.24f, bodyMat, renderers);

            CreatePrimitivePart(parent, "Shield", PrimitiveType.Cube, new Vector3(-0.34f, 0.42f, 0.05f), Quaternion.Euler(0, 20f, 0), new Vector3(0.06f, 0.5f, 0.34f), accentMat, renderers);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void BuildWind(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var bodyMat = ToonMaterialFactory.Instance(visual.BaseColor);
            var accentMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 1.2f, softEdge: 0.25f);

            CreatePrimitivePart(parent, "Body", PrimitiveType.Capsule, new Vector3(0, 0.5f, 0), Quaternion.identity, new Vector3(0.32f, 0.5f, 0.32f), bodyMat, renderers);

            var hoodMesh = PrimitiveMeshFactory.Cone(6, 0.14f, 0f, 0.22f);
            CreatePart(parent, "Hood", hoodMesh, new Vector3(0, 1.0f, 0), Quaternion.identity, Vector3.one, bodyMat, renderers);

            var finMesh = PrimitiveMeshFactory.Cone(4, 0.04f, 0f, 0.26f);
            CreatePart(parent, "FinLeft", finMesh, new Vector3(-0.15f, 0.78f, -0.02f), Quaternion.Euler(70f, 20f, 60f), Vector3.one, accentMat, renderers);
            CreatePart(parent, "FinRight", finMesh, new Vector3(0.15f, 0.78f, -0.02f), Quaternion.Euler(70f, -20f, -60f), Vector3.one, accentMat, renderers);

            // Flat/horizontal (no tilt) so it reads as wind swirling around the ankle and
            // so spinning it around local up matches its visual orbit, not a tumble.
            var ringMesh = PrimitiveMeshFactory.Torus(0.22f, 0.025f, 16, 6);
            var ring = CreatePart(parent, "AnkleRing", ringMesh, new Vector3(0, 0.08f, 0), Quaternion.identity, Vector3.one, accentMat, renderers);
            animator.Register(ring.transform, bobAmount: 0f, spinSpeed: 90f);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void BuildArcane(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var bodyMat = ToonMaterialFactory.Instance(visual.BaseColor);
            var glowMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 1.8f, softEdge: 0.3f);

            // Body doesn't touch the ground — the gap plus the ground disc below sell the hover.
            var coneMesh = PrimitiveMeshFactory.Cone(8, 0.04f, 0.22f, 0.5f);
            CreatePart(parent, "Body", coneMesh, new Vector3(0, 0.15f, 0), Quaternion.identity, Vector3.one, bodyMat, renderers);

            CreatePrimitivePart(parent, "Head", PrimitiveType.Sphere, new Vector3(0, 0.8f, 0), Quaternion.identity, Vector3.one * 0.22f, bodyMat, renderers);

            var haloMesh = PrimitiveMeshFactory.Torus(0.16f, 0.02f, 16, 6);
            var haloA = CreatePart(parent, "HaloA", haloMesh, new Vector3(0, 0.95f, 0), Quaternion.Euler(70f, 0f, 0f), Vector3.one, glowMat, renderers);
            var haloB = CreatePart(parent, "HaloB", haloMesh, new Vector3(0, 0.9f, 0), Quaternion.Euler(20f, 50f, 0f), Vector3.one * 0.85f, glowMat, renderers);
            animator.Register(haloA.transform, bobAmount: 0f, spinSpeed: 40f);
            animator.Register(haloB.transform, bobAmount: 0f, spinSpeed: -55f);

            var orb = CreatePrimitivePart(parent, "Orb", PrimitiveType.Sphere, new Vector3(0, 0.55f, 0.22f), Quaternion.identity, Vector3.one * 0.09f, glowMat, renderers);
            animator.Register(orb.transform, bobSpeed: 1.5f, bobAmount: 0.03f);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void AddGroundDisc(Transform parent, Color color, List<Renderer> renderers, float radius = 0.4f)
        {
            var mat = ToonMaterialFactory.GlowInstance(color, intensity: 0.6f, softEdge: 0.15f);
            CreatePrimitivePart(parent, "GroundDisc", PrimitiveType.Cylinder, new Vector3(0, 0.015f, 0), Quaternion.identity,
                new Vector3(radius * 2f, 0.015f, radius * 2f), mat, renderers);
        }

        private static GameObject CreatePart(Transform parent, string name, Mesh mesh, Vector3 localPos, Quaternion localRot,
            Vector3 localScale, Material material, List<Renderer> renderers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return go;
        }

        private static GameObject CreatePrimitivePart(Transform parent, string name, PrimitiveType type, Vector3 localPos,
            Quaternion localRot, Vector3 localScale, Material material, List<Renderer> renderers)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Object.Destroy(go.GetComponent<Collider>());
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderers.Add(renderer);
            return go;
        }
    }
}
