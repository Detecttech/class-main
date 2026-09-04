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

    public static class CharacterVisualBuilder
    {
        private static readonly Dictionary<CharacterArchetype, string> ImportedModelResourcePaths = new Dictionary<CharacterArchetype, string>
        {
            { CharacterArchetype.Fire, "Characters/Models/Costume_13_001" },
            { CharacterArchetype.Tank, "Characters/Models/Mascot_002" },
            { CharacterArchetype.Wind, "Characters/Models/Base_Mesh" },
            { CharacterArchetype.Arcane, "Characters/Models/Base_Mesh" },
        };

        private const float ImportedBodyHeight = 1.0f;
        private const string CharacterAtlasResourcePath = "Textures/CharacterAtlas";

        private static Texture2D _cachedAtlas;
        private static bool _atlasResolved;

        public static CharacterVisualResult Build(in CharacterVisual visual, Transform parent)
        {
            var renderers = new List<Renderer>();
            var animator = parent.gameObject.AddComponent<TokenIdleAnimator>();
            var bodyRoot = new GameObject("CharacterBodyVisual").transform;
            bodyRoot.SetParent(parent, false);

            switch (visual.Archetype)
            {
            case CharacterArchetype.Fire:
                BuildFire(bodyRoot, visual, renderers, animator);
                break;
            case CharacterArchetype.Tank:
                BuildTank(bodyRoot, visual, renderers, animator);
                break;
            case CharacterArchetype.Wind:
                BuildWind(bodyRoot, visual, renderers, animator);
                break;
            case CharacterArchetype.Arcane:
                BuildArcane(bodyRoot, visual, renderers, animator);
                break;
            default:
                BuildGeneric(bodyRoot, visual, renderers);
                break;
            }

            bodyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            animator.SetBodyRoot(bodyRoot);
            AddGroundDisc(parent, visual.BaseColor, renderers);
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

        private static GameObject BuildImportedBody(Transform parent, CharacterArchetype archetype, List<Renderer> renderers)
        {
            if (!ImportedModelResourcePaths.TryGetValue(archetype, out var resourcePath)) return null;

            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterVisualBuilder] imported model '{resourcePath}' not found; using a fallback body for {archetype}.");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = "Body";
            PoseImportedArms(instance.transform, parent);

            var enabledRenderers = new List<Renderer>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (SkippedDefaultParts.Contains(r.name)) r.enabled = false;
                else enabledRenderers.Add(r);
            }
            var bodyRenderers = enabledRenderers.ToArray();
            if (bodyRenderers.Length == 0)
            {
                Object.Destroy(instance);
                return null;
            }

            var bounds = GetLocalBounds(parent, bodyRenderers);
            float rawHeight = Mathf.Max(bounds.size.y, 0.001f);
            instance.transform.localScale *= ImportedBodyHeight / rawHeight;

            bounds = GetLocalBounds(parent, bodyRenderers);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

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
                    piece.transform.localRotation = instance.transform.localRotation;
                    PoseImportedArms(piece.transform, parent);
                    var pieceRenderers = piece.GetComponentsInChildren<Renderer>();
                    ApplyNaturalMaterial(pieceRenderers, naturalMat, renderers);
                }
            }

            return instance;
        }

        private static void PoseImportedArms(Transform instance, Transform parent)
        {
            var bones = instance.GetComponentsInChildren<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                Transform arm = null;
                Transform elbow = null;
                Transform hand = null;
                foreach (var bone in bones)
                {
                    if (bone.name == prefix + "Arm") arm = bone;
                    else if (bone.name == prefix + "ForeArm") elbow = bone;
                    else if (bone.name == prefix + "Hand") hand = bone;
                }
                if (arm == null || elbow == null || hand == null) continue;
                var upperDirection = parent.TransformDirection(new Vector3(side * 0.5f, -0.866f, 0f));
                arm.rotation = Quaternion.FromToRotation(elbow.position - arm.position, upperDirection) * arm.rotation;
                var lowerDirection = parent.TransformDirection(new Vector3(side * 0.2f, -0.97f, 0.12f));
                elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, lowerDirection) * elbow.rotation;
            }
        }

        private static Vector3 BodyPoint(Transform parent, string boneName, Vector3 fallback)
        {
            var body = parent.Find("Body");
            if (body != null)
                foreach (var bone in body.GetComponentsInChildren<Transform>())
                    if (bone.name == boneName) return parent.InverseTransformPoint(bone.position);
            return fallback;
        }

        private static Bounds GetLocalBounds(Transform parent, Renderer[] renderers)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var renderer in renderers)
            {
                var localBounds = renderer.localBounds;
                var matrix = parent.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = localBounds.center + Vector3.Scale(localBounds.extents,
                                 new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!initialized)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
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
        }

        private static void BuildFire(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            if (BuildImportedBody(parent, CharacterArchetype.Fire, renderers) == null) BuildGeneric(parent, visual, renderers);
            var armorMat = EquipmentMaterial(Color.Lerp(visual.BaseColor, new Color(0.18f, 0.06f, 0.04f), 0.6f));
            var trimMat = EquipmentMaterial(visual.AccentColor);
            var emberMat = EquipmentMaterial(visual.BaseColor, visual.EmissionColor, 0.28f);
            var gauntletMesh = PrimitiveMeshFactory.Cone(6, 0.105f, 0.08f, 0.18f);
            var cuffMesh = PrimitiveMeshFactory.Cone(6, 0.085f, 0.095f, 0.045f);
            var flameMesh = PrimitiveMeshFactory.Cone(5, 0.065f, 0f, 0.22f);

            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                var hand = BodyPoint(parent, prefix + "Hand", new Vector3(side * 0.23f, 0.43f, 0.015f));
                var elbow = BodyPoint(parent, prefix + "ForeArm", new Vector3(side * 0.2f, 0.56f, 0f));
                var direction = (elbow - hand).normalized;
                var gauntlet = CreatePart(parent, $"FireGauntlet_{side}", gauntletMesh,
                                          hand - direction * 0.065f, Quaternion.FromToRotation(Vector3.up, direction),
                                          new Vector3(1f, 1f, 0.85f), armorMat, renderers).transform;
                CreatePart(gauntlet, "GoldCuff", cuffMesh, new Vector3(0f, 0.145f, 0f), Quaternion.identity, Vector3.one, trimMat, renderers);
                CreatePrimitivePart(gauntlet, "EmberKnuckle", PrimitiveType.Sphere, new Vector3(0f, 0.065f, 0.08f),
                                    Quaternion.identity, new Vector3(0.095f, 0.08f, 0.04f), emberMat, renderers);
                CreatePart(gauntlet, "FlameGuard", flameMesh, new Vector3(side * 0.06f, 0.08f, 0f),
                           Quaternion.Euler(-18f, 0f, -side * 20f), new Vector3(0.5f, 0.55f, 0.6f), trimMat, renderers);
                CreatePrimitivePart(parent, $"FirePauldron_{side}", PrimitiveType.Sphere,
                                    BodyPoint(parent, prefix + "Arm", new Vector3(side * 0.18f, 0.68f, 0f)) + new Vector3(side * 0.025f, -0.025f, 0f),
                                    Quaternion.Euler(0f, 0f, side * -18f),
                                    new Vector3(0.18f, 0.11f, 0.22f), armorMat, renderers);
            }

            CreatePart(parent, "FlameCrestCenter", flameMesh, new Vector3(0f, 0.96f, -0.025f),
                       Quaternion.Euler(-20f, 0f, 0f), new Vector3(1f, 1f, 0.65f), emberMat, renderers);
            for (int side = -1; side <= 1; side += 2)
                CreatePart(parent, $"FlameCrest_{side}", flameMesh, new Vector3(side * 0.085f, 0.93f, -0.025f),
                           Quaternion.Euler(-15f, 0f, -side * 24f), new Vector3(0.7f, 0.72f, 0.55f), trimMat, renderers);

            var ember = CreatePart(parent, "FloatingEmber", PrimitiveMeshFactory.Cone(4, 0.025f, 0f, 0.065f),
                                   new Vector3(0.36f, 0.64f, 0.02f), Quaternion.Euler(0f, 45f, -12f), Vector3.one, emberMat, renderers);
            animator.Register(ember.transform, bobSpeed: 1.6f, bobAmount: 0.015f, spinSpeed: 24f);
        }

        private static void BuildTank(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            if (BuildImportedBody(parent, CharacterArchetype.Tank, renderers) == null) BuildGeneric(parent, visual, renderers);
            var armorMat = EquipmentMaterial(Color.Lerp(visual.BaseColor, new Color(0.06f, 0.12f, 0.23f), 0.55f));
            var trimMat = EquipmentMaterial(visual.AccentColor);
            var enamelMat = EquipmentMaterial(visual.BaseColor);

            CreatePrimitivePart(parent, "ChestPlate", PrimitiveType.Sphere, new Vector3(0f, 0.53f, 0.14f),
                                Quaternion.identity, new Vector3(0.32f, 0.26f, 0.13f), armorMat, renderers);
            CreatePrimitivePart(parent, "ChestSigil", PrimitiveType.Cube, new Vector3(0f, 0.56f, 0.205f),
                                Quaternion.Euler(0f, 0f, 45f), new Vector3(0.065f, 0.065f, 0.022f), trimMat, renderers);
            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                var shoulder = CreatePart(parent, $"TankPauldron_{side}", PrimitiveMeshFactory.Cone(6, 0.12f, 0.085f, 0.11f),
                                          BodyPoint(parent, prefix + "Arm", new Vector3(side * 0.22f, 0.68f, 0f)) + new Vector3(side * 0.03f, -0.075f, 0f),
                                          Quaternion.Euler(0f, 30f, side * -16f),
                                          new Vector3(1f, 1f, 1.15f), armorMat, renderers).transform;
                CreatePart(shoulder, "ShoulderRim", PrimitiveMeshFactory.Cone(6, 0.125f, 0.12f, 0.025f),
                           Vector3.zero, Quaternion.identity, Vector3.one, trimMat, renderers);
                CreatePrimitivePart(parent, $"Greave_{side}", PrimitiveType.Cube,
                                    new Vector3(side * 0.105f, 0.19f, 0.09f), Quaternion.Euler(-8f, 0f, side * -5f),
                                    new Vector3(0.12f, 0.16f, 0.07f), armorMat, renderers);
            }

            var shield = new GameObject("Shield").transform;
            shield.SetParent(parent, false);
            shield.localPosition = BodyPoint(parent, "LeftHand", new Vector3(-0.255f, 0.45f, 0.06f)) + new Vector3(-0.055f, 0.04f, 0.1f);
            shield.localRotation = Quaternion.Euler(8f, -18f, -8f);
            var shieldMesh = PrimitiveMeshFactory.Cone(6, 0.5f, 0.46f, 0.07f);
            CreatePart(shield, "ShieldRim", shieldMesh, Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
                       new Vector3(0.38f, 1f, 0.55f), trimMat, renderers);
            CreatePart(shield, "ShieldFace", shieldMesh, new Vector3(0f, 0f, 0.035f), Quaternion.Euler(90f, 0f, 0f),
                       new Vector3(0.325f, 0.65f, 0.48f), enamelMat, renderers);
            CreatePrimitivePart(shield, "ShieldBoss", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.095f),
                                Quaternion.identity, new Vector3(0.12f, 0.12f, 0.065f), trimMat, renderers);
            CreatePrimitivePart(shield, "ShieldSpine", PrimitiveType.Cube, new Vector3(0f, 0f, 0.075f),
                                Quaternion.identity, new Vector3(0.022f, 0.36f, 0.018f), trimMat, renderers);
        }

        private static void BuildWind(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            if (BuildImportedBody(parent, CharacterArchetype.Wind, renderers) == null) BuildGeneric(parent, visual, renderers);
            var armorMat = EquipmentMaterial(Color.Lerp(visual.BaseColor, new Color(0.035f, 0.18f, 0.17f), 0.6f));
            var bladeMat = EquipmentMaterial(visual.AccentColor);
            var jadeMat = EquipmentMaterial(visual.BaseColor, visual.EmissionColor, 0.12f);
            var finMesh = PrimitiveMeshFactory.Cone(4, 0.07f, 0f, 0.3f);

            CreatePrimitivePart(parent, "WindHarness", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.13f),
                                Quaternion.Euler(0f, 0f, -28f), new Vector3(0.065f, 0.27f, 0.045f), armorMat, renderers);
            CreatePrimitivePart(parent, "JadeClasp", PrimitiveType.Cube, new Vector3(0.015f, 0.57f, 0.16f),
                                Quaternion.Euler(0f, 0f, 45f), new Vector3(0.055f, 0.055f, 0.035f), jadeMat, renderers);
            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                var shoulder = BodyPoint(parent, prefix + "Arm", new Vector3(side * 0.18f, 0.68f, 0f));
                CreatePrimitivePart(parent, $"WindShoulder_{side}", PrimitiveType.Sphere,
                                    shoulder + new Vector3(side * 0.025f, -0.02f, -0.035f), Quaternion.Euler(0f, 0f, side * -20f),
                                    new Vector3(0.145f, 0.09f, 0.16f), armorMat, renderers);
                CreatePart(parent, $"SweptFin_{side}", finMesh, shoulder + new Vector3(side * 0.04f, 0f, -0.065f),
                           Quaternion.Euler(-32f, side * 20f, -side * 38f), new Vector3(1f, 1f, 0.38f), bladeMat, renderers);
                CreatePart(parent, $"LowerFin_{side}", finMesh, shoulder + new Vector3(side * 0.065f, -0.03f, -0.065f),
                           Quaternion.Euler(-42f, side * 20f, -side * 60f), new Vector3(0.65f, 0.65f, 0.32f), jadeMat, renderers);
                var hand = BodyPoint(parent, prefix + "Hand", new Vector3(side * 0.23f, 0.43f, 0.015f));
                var elbow = BodyPoint(parent, prefix + "ForeArm", new Vector3(side * 0.2f, 0.56f, 0f));
                var direction = (elbow - hand).normalized;
                var bracer = CreatePart(parent, $"WindBracer_{side}", PrimitiveMeshFactory.Cone(6, 0.065f, 0.06f, 0.14f),
                                        hand - direction * 0.02f, Quaternion.FromToRotation(Vector3.up, direction),
                                        Vector3.one, armorMat, renderers).transform;
                CreatePart(bracer, "ForearmBlade", finMesh, new Vector3(side * 0.045f, 0.04f, 0.01f),
                           Quaternion.Euler(0f, 0f, -side * 15f), new Vector3(0.55f, 0.75f, 0.3f), bladeMat, renderers);
                CreatePart(parent, $"HeelFin_{side}", finMesh, new Vector3(side * 0.095f, 0.1f, -0.07f),
                           Quaternion.Euler(-50f, 0f, -side * 20f), new Vector3(0.5f, 0.42f, 0.35f), jadeMat, renderers);
            }
        }

        private static void BuildArcane(Transform parent, in CharacterVisual visual, List<Renderer> renderers, TokenIdleAnimator animator)
        {
            if (BuildImportedBody(parent, CharacterArchetype.Arcane, renderers) == null) BuildGeneric(parent, visual, renderers);
            var armorMat = EquipmentMaterial(Color.Lerp(visual.BaseColor, new Color(0.09f, 0.055f, 0.2f), 0.7f));
            var trimMat = EquipmentMaterial(new Color(0.84f, 0.69f, 0.4f));
            var crystalMat = EquipmentMaterial(visual.AccentColor, visual.EmissionColor, 0.24f);

            for (int side = -1; side <= 1; side += 2)
            {
                string prefix = side < 0 ? "Left" : "Right";
                CreatePart(parent, $"ArcaneMantle_{side}", PrimitiveMeshFactory.Cone(5, 0.1f, 0.055f, 0.095f),
                           BodyPoint(parent, prefix + "Arm", new Vector3(side * 0.15f, 0.7f, 0f)) + new Vector3(side * 0.02f, -0.07f, -0.015f),
                           Quaternion.Euler(-12f, 0f, side * -25f),
                           new Vector3(1f, 1f, 1.1f), armorMat, renderers);
                CreatePrimitivePart(parent, $"MantleClasp_{side}", PrimitiveType.Sphere,
                                    new Vector3(side * 0.125f, 0.65f, 0.105f), Quaternion.identity,
                                    new Vector3(0.055f, 0.055f, 0.03f), trimMat, renderers);
            }
            CreatePrimitivePart(parent, "ArcaneBelt", PrimitiveType.Cube, new Vector3(0f, 0.4f, 0.12f),
                                Quaternion.identity, new Vector3(0.23f, 0.045f, 0.055f), armorMat, renderers);
            CreateCrystal(parent, "BeltFocus", new Vector3(0f, 0.41f, 0.155f), new Vector3(0.04f, 0.07f, 0.025f), crystalMat, renderers);

            var staff = new GameObject("ArcaneStaff").transform;
            staff.SetParent(parent, false);
            staff.localRotation = Quaternion.Euler(-5f, 0f, -7f);
            staff.localPosition = BodyPoint(parent, "RightHand", new Vector3(0.27f, 0.45f, 0.035f))
                                  - staff.localRotation * new Vector3(0f, 0.33f, 0f);
            CreatePart(staff, "StaffShaft", PrimitiveMeshFactory.Cone(8, 0.018f, 0.014f, 0.75f),
                       Vector3.zero, Quaternion.identity, Vector3.one, armorMat, renderers);
            var ferruleMesh = PrimitiveMeshFactory.Cone(8, 0.023f, 0.023f, 0.045f);
            CreatePart(staff, "StaffFoot", ferruleMesh, Vector3.zero, Quaternion.identity, Vector3.one, trimMat, renderers);
            CreatePart(staff, "StaffGrip", ferruleMesh, new Vector3(0f, 0.29f, 0f), Quaternion.identity,
                       new Vector3(1f, 2f, 1f), trimMat, renderers);
            CreatePart(staff, "FocusCrown", PrimitiveMeshFactory.Torus(0.075f, 0.012f, 12, 5),
                       new Vector3(0f, 0.79f, 0f), Quaternion.Euler(65f, 0f, 0f), Vector3.one, trimMat, renderers);
            var focus = CreateCrystal(staff, "StaffCrystal", new Vector3(0f, 0.82f, 0f),
                                      new Vector3(0.062f, 0.17f, 0.062f), crystalMat, renderers);
            animator.Register(focus, bobSpeed: 1.1f, bobAmount: 0.008f, spinSpeed: 25f);

            var orbit = new GameObject("CrystalOrbit").transform;
            orbit.SetParent(parent, false);
            orbit.localPosition = new Vector3(0f, 1.04f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                CreateCrystal(orbit, $"OrbitCrystal_{i}", new Vector3(Mathf.Cos(angle) * 0.205f, i == 1 ? 0.035f : 0f, Mathf.Sin(angle) * 0.205f),
                              new Vector3(0.03f, 0.075f, 0.03f), crystalMat, renderers);
            }
            animator.Register(orbit, bobSpeed: 0.9f, bobAmount: 0.008f, spinSpeed: -18f);
        }

        private static Material EquipmentMaterial(Color color, Color emission = default, float emissionIntensity = 0f)
        {
            var style = ToonStyle.Default;
            style.RimIntensity = 0.45f;
            style.SpecIntensity = 0.55f;
            style.Gloss = 32f;
            style.OutlineWidth = 1.25f;
            style.EmissionColor = emission;
            style.EmissionIntensity = emissionIntensity;
            return ToonMaterialFactory.Instance(color, style);
        }

        private static Transform CreateCrystal(Transform parent, string name, Vector3 position, Vector3 scale,
                                               Material material, List<Renderer> renderers)
        {
            var crystal = new GameObject(name).transform;
            crystal.SetParent(parent, false);
            crystal.localPosition = position;
            crystal.localScale = scale;
            var mesh = PrimitiveMeshFactory.Cone(5, 1f, 0f, 0.5f);
            CreatePart(crystal, "CrystalTip", mesh, Vector3.zero, Quaternion.identity, Vector3.one, material, renderers);
            CreatePart(crystal, "CrystalBase", mesh, Vector3.zero, Quaternion.Euler(180f, 0f, 0f), Vector3.one, material, renderers);
            return crystal;
        }

        private static void AddGroundDisc(Transform parent, Color color, List<Renderer> renderers, float radius = 0.4f)
        {
            var ringMat = ToonMaterialFactory.GlowInstance(color, intensity: 0.85f, softEdge: 0.2f);
            var ringMesh = PrimitiveMeshFactory.Torus(radius * 1.05f, 0.018f, 20, 6);
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
