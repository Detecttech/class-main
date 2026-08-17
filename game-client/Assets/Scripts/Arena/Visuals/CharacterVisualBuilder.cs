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

    /// Builds a distinct silhouette per CharacterArchetype. Shared rules across every
    /// archetype: origin at the feet (y=0 is the ground contact point, matching
    /// GridController.TileToWorldPos), total height stays under ~1.3 tile-units so tokens
    /// read clearly on an 8-wide board, and every character gets a thin emissive ground
    /// disc in its own color — a cheap silhouette-independent position/team readability cue.
    ///
    /// Torsos/heads are imported ithappy character models (see ImportedModelResourcePaths)
    /// recolored via QB_Toon; the flame crests/chest plates/fins/halos etc. below are the
    /// original procedural accent parts, unchanged, just re-anchored onto the imported
    /// body instead of a primitive torso.
    public static class CharacterVisualBuilder
    {
        // Only 3 distinct body meshes are available in the free character pack, so Wind
        // and Arcane share Base_Mesh — differentiated by BaseColor/AccentColor tint and
        // their own accent parts, same as the rest of this file already does per-archetype.
        private static readonly Dictionary<CharacterArchetype, string> ImportedModelResourcePaths = new Dictionary<CharacterArchetype, string>
        {
            { CharacterArchetype.Fire, "Characters/Models/Costume_13_001" },
            { CharacterArchetype.Tank, "Characters/Models/Mascot_002" },
            { CharacterArchetype.Wind, "Characters/Models/Base_Mesh" },
            { CharacterArchetype.Arcane, "Characters/Models/Base_Mesh" },
        };

        // Roughly matches the total height the old primitive-built torsos/heads occupied
        // (~0.9-1.1 units), so the accent-part offsets below — authored against that old
        // scale — still land in about the right place without per-bone repositioning math.
        private const float ImportedBodyHeight = 1.0f;
        private const string CharacterAtlasResourcePath = "Textures/CharacterAtlas";

        private static Texture2D _cachedAtlas;
        private static bool _atlasResolved;

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
                    BuildTank(parent, visual, renderers, animator);
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

        private static Texture2D ResolveCharacterAtlas()
        {
            if (_atlasResolved) return _cachedAtlas;
            _cachedAtlas = Resources.Load<Texture2D>(CharacterAtlasResourcePath);
            _atlasResolved = true;
            return _cachedAtlas;
        }

        // Base_Mesh ships with every optional slot (hat AND hairstyle AND mustache AND
        // glasses AND a redundant full-body coverall on top of the separate shirt/pants)
        // switched on simultaneously — the pack's own customization tool is what normally
        // turns these off/on; instantiated raw, they z-fight into a jumbled mess. Disabling
        // the overlapping/extraneous ones leaves a clean base. Base_Mesh's own embedded
        // T_Shirt/Pants/Outerwear/Shoes/Hairstyle are also blank/neutral placeholders (the
        // pack's actual colored art lives in the separate named wardrobe prefabs below), so
        // those are hidden too and replaced with real equipped pieces.
        private static readonly HashSet<string> SkippedDefaultParts = new HashSet<string>
        {
            "Full_body", "Hat", "Mustache", "Glasses", "Accessories",
            "T_Shirt", "Pants", "Outerwear", "Shoes", "Hairstyle",
        };

        // Standalone colored wardrobe pieces (each a self-contained mesh+skeleton at the
        // same rig scale as Base_Mesh) equipped onto the Wind/Arcane archetypes in place of
        // Base_Mesh's own blank defaults — Fire/Tank use Costume_13_001/Mascot_002, which
        // are already single fully-colored meshes and need no additional wardrobe. Wind and
        // Arcane both use Base_Mesh as their base, so they get deliberately different
        // combos here — otherwise they'd be visually identical apart from accent props.
        private static readonly Dictionary<CharacterArchetype, string[]> ImportedWardrobeResourcePaths = new Dictionary<CharacterArchetype, string[]>
        {
            { CharacterArchetype.Wind, new[] { "Characters/Models/Outfit_010", "Characters/Models/Pants_009", "Characters/Models/Hairstyle_Male_001", "Characters/Models/Shoe_Sneakers_009" } },
            { CharacterArchetype.Arcane, new[] { "Characters/Models/Outwear_004", "Characters/Models/Pants_010", "Characters/Models/Hairstyle_Male_005", "Characters/Models/Shoe_Slippers_002" } },
        };

        /// Instantiates the archetype's imported body model, scales it to
        /// ImportedBodyHeight, and re-anchors it so feet sit at local y=0 (origin-at-feet,
        /// matching every primitive-built archetype). Renderers keep the pack's own
        /// natural colors/textures (no per-archetype tint) so characters look like the
        /// pack's actual art instead of a flat-dyed silhouette — archetype color-coding
        /// comes from the accent props (flame crest, halo, chest plate, ...), the ground
        /// disc, and the HP bar/nameplate instead. Materials are still swapped to QB_Toon
        /// (carrying the shared UV atlas through via the new _MainTex support) so the
        /// outline pass, cel-shading, and CharacterToken.SetEliminated's dimming all keep
        /// working uniformly instead of mixing in the pack's own shader.
        /// Returns null (falls back to no imported body) if the archetype has no mapped
        /// model or the resource failed to load.
        private static GameObject BuildImportedBody(Transform parent, CharacterArchetype archetype, List<Renderer> renderers)
        {
            if (!ImportedModelResourcePaths.TryGetValue(archetype, out var resourcePath)) return null;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterVisualBuilder] imported model '{resourcePath}' not found — {archetype} will have no body.");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = "Body";

            var enabledRenderers = new List<Renderer>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (SkippedDefaultParts.Contains(r.name)) r.enabled = false;
                else enabledRenderers.Add(r);
            }
            var bodyRenderers = enabledRenderers.ToArray();
            if (bodyRenderers.Length == 0) return instance;

            var bounds = bodyRenderers[0].bounds;
            foreach (var r in bodyRenderers) bounds.Encapsulate(r.bounds);
            float rawHeight = Mathf.Max(bounds.size.y, 0.001f);
            instance.transform.localScale = Vector3.one * (ImportedBodyHeight / rawHeight);

            // Re-measure after scaling — bounds.min.y shifts non-linearly with scale
            // around an arbitrary pivot, so this can't be precomputed before the scale is set.
            bounds = bodyRenderers[0].bounds;
            foreach (var r in bodyRenderers) bounds.Encapsulate(r.bounds);
            float feetGap = bounds.min.y - parent.position.y;
            instance.transform.localPosition = new Vector3(0f, -feetGap, 0f);

            var atlas = ResolveCharacterAtlas();
            var naturalMat = atlas != null
                ? ToonMaterialFactory.Instance(Color.white, ToonStyle.Default, atlas, new Vector4(1f, 1f, 0f, 0f))
                : ToonMaterialFactory.Instance(Color.white);

            ApplyNaturalMaterial(bodyRenderers, naturalMat, renderers);

            if (ImportedWardrobeResourcePaths.TryGetValue(archetype, out var wardrobePaths))
            {
                foreach (var wardrobePath in wardrobePaths)
                {
                    var wardrobePrefab = Resources.Load<GameObject>(wardrobePath);
                    if (wardrobePrefab == null)
                    {
                        Debug.LogWarning($"[CharacterVisualBuilder] wardrobe piece '{wardrobePath}' not found — skipping.");
                        continue;
                    }

                    // Every wardrobe piece is a self-contained mesh+skeleton built at the
                    // same rig scale as Base_Mesh, so matching the main body's already-
                    // computed scale/position aligns it correctly with no bone remapping.
                    var piece = Object.Instantiate(wardrobePrefab, parent, false);
                    piece.transform.localScale = instance.transform.localScale;
                    piece.transform.localPosition = instance.transform.localPosition;
                    var pieceRenderers = piece.GetComponentsInChildren<Renderer>();
                    ApplyNaturalMaterial(pieceRenderers, naturalMat, renderers);
                }
            }

            return instance;
        }

        private static void ApplyNaturalMaterial(Renderer[] targets, Material naturalMat, List<Renderer> renderers)
        {
            foreach (var r in targets)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = naturalMat;
                r.sharedMaterials = mats;
                renderers.Add(r);
            }
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
            var emberMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 2.5f, softEdge: 0.3f);

            var body = BuildImportedBody(parent, CharacterArchetype.Fire, renderers);
            if (body != null) animator.SetBodyRoot(body.transform);

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

        private static void BuildTank(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var accentMat = ToonMaterialFactory.Instance(visual.AccentColor);

            var body = BuildImportedBody(parent, CharacterArchetype.Tank, renderers);
            if (body != null) animator.SetBodyRoot(body.transform);

            CreatePrimitivePart(parent, "ChestPlate", PrimitiveType.Cube, new Vector3(0, 0.5f, 0.15f), Quaternion.identity, new Vector3(0.36f, 0.28f, 0.06f), accentMat, renderers);

            CreatePrimitivePart(parent, "ShoulderLeft", PrimitiveType.Cube, new Vector3(-0.3f, 0.62f, 0), Quaternion.Euler(0, 0, 16f), new Vector3(0.18f, 0.14f, 0.24f), accentMat, renderers);
            CreatePrimitivePart(parent, "ShoulderRight", PrimitiveType.Cube, new Vector3(0.3f, 0.62f, 0), Quaternion.Euler(0, 0, -16f), new Vector3(0.18f, 0.14f, 0.24f), accentMat, renderers);

            CreatePrimitivePart(parent, "Shield", PrimitiveType.Cube, new Vector3(-0.34f, 0.42f, 0.05f), Quaternion.Euler(0, 20f, 0), new Vector3(0.06f, 0.5f, 0.34f), accentMat, renderers);

            AddGroundDisc(parent, visual.BaseColor, renderers);
        }

        private static void BuildWind(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            var accentMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 1.2f, softEdge: 0.25f);

            var body = BuildImportedBody(parent, CharacterArchetype.Wind, renderers);
            if (body != null) animator.SetBodyRoot(body.transform);

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
            var glowMat = ToonMaterialFactory.GlowInstance(visual.EmissionColor, intensity: 1.8f, softEdge: 0.3f);

            var body = BuildImportedBody(parent, CharacterArchetype.Arcane, renderers);
            if (body != null) animator.SetBodyRoot(body.transform);

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
            // Soft dark contact blob shadow on the grass floor
            var shadowMat = ToonMaterialFactory.GlowInstance(new Color(0.02f, 0.02f, 0.04f), intensity: 0.85f, softEdge: 0.6f);
            CreatePrimitivePart(parent, "BlobShadow", PrimitiveType.Cylinder, new Vector3(0, 0.012f, 0), Quaternion.identity,
                new Vector3(radius * 2.2f, 0.008f, radius * 2.2f), shadowMat, renderers);

            // Stylized golden & team-tinted metallic base ring
            var ringMat = ToonMaterialFactory.GlowInstance(color, intensity: 1.3f, softEdge: 0.2f);
            var ringMesh = PrimitiveMeshFactory.Torus(radius * 1.05f, 0.03f, 20, 6);
            CreatePart(parent, "TeamBaseRing", ringMesh, new Vector3(0, 0.022f, 0), Quaternion.identity, Vector3.one, ringMat, renderers);
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
