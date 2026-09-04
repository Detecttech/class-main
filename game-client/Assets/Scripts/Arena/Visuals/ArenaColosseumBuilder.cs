using UnityEngine;
using UnityEngine.Rendering;

namespace QuizBattle.Arena.Visuals
{
    public static class ArenaColosseumBuilder
    {
        public static GameObject Build(Transform parent, int width, int height, float tileSize)
        {
            var root = new GameObject("Colosseum_Environment");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3((width - 1) * tileSize * 0.5f, 0f, (height - 1) * tileSize * 0.5f);
            float halfW = width * tileSize * 0.5f;
            float halfH = height * tileSize * 0.5f;

            var style = ToonStyle.Default;
            style.OutlineEnabled = false;
            style.RimIntensity = 0.25f;
            style.SpecIntensity = 0.18f;
            var stone = ToonMaterialFactory.Toon(QuizBattlePalette.StoneWall, style);
            var dark = ToonMaterialFactory.Toon(QuizBattlePalette.StoneDark, style);
            var edge = ToonMaterialFactory.Toon(QuizBattlePalette.StoneBorder, style);
            var gold = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, style);
            var violet = ToonMaterialFactory.Toon(QuizBattlePalette.ArcaneViolet, style);
            var ember = ToonMaterialFactory.Toon(QuizBattlePalette.BannerRed, style);
            style.EmissionColor = QuizBattlePalette.ArenaCyan;
            style.EmissionIntensity = 0.55f;
            var cyan = ToonMaterialFactory.Toon(QuizBattlePalette.ArenaCyan, style);

            var stadium = new GameObject("LayeredStadium");
            stadium.transform.SetParent(root.transform, false);
            var frame = stadium.transform;
            Box(frame, "FloatingFoundation", new Vector3(0f, -2.1f, 0.6f), new Vector3(halfW * 2f + 7.2f, 0.65f, halfH * 2f + 5.4f), dark);
            Box(frame, "FoundationGoldSeam", new Vector3(0f, -1.74f, 0.6f), new Vector3(halfW * 2f + 6.8f, 0.08f, halfH * 2f + 5f), gold);
            Box(frame, "TournamentDeck", new Vector3(0f, -1.4f, 0.6f), new Vector3(halfW * 2f + 6.6f, 0.55f, halfH * 2f + 4.8f), stone);
            Box(frame, "OpenArrivalApron", new Vector3(0f, -0.82f, -halfH - 1.25f), new Vector3(halfW * 2f + 2f, 0.25f, 1.5f), dark);
            Box(frame, "ArrivalCyanInlay", new Vector3(0f, -0.68f, -halfH - 1.5f), new Vector3(halfW * 2f, 0.025f, 0.08f), cyan);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    float x = side * (halfW + 1.15f + tier * 0.7f);
                    float top = -0.65f + tier * 0.22f;
                    Box(frame, "FlankTerrace", new Vector3(x, top - 0.22f, 0f), new Vector3(0.65f, 0.44f, halfH * 2f + 0.8f), tier == 1 ? stone : dark);
                    Box(frame, "TerraceTrim", new Vector3(x, top + 0.015f, 0f), new Vector3(0.08f, 0.025f, halfH * 2f + 0.8f), tier == 1 ? gold : cyan);
                }

                Box(frame, "LowArrivalBastion", new Vector3(side * (halfW + 2.2f), -0.75f, -halfH - 1.2f), new Vector3(1.3f, 0.65f, 1.25f), stone);
                Box(frame, "ArrivalBastionCap", new Vector3(side * (halfW + 2.2f), -0.39f, -halfH - 1.2f), new Vector3(1.42f, 0.06f, 1.36f), gold);

                for (int tier = 0; tier < 3; tier++)
                {
                    Box(frame, "RearGrandstand", new Vector3(side * (halfW * 0.5f + 1.5f), 0.15f + tier * 0.42f, halfH + 1.3f + tier * 0.7f),
                        new Vector3(halfW + 0.6f, 0.7f, 0.65f), tier == 1 ? stone : dark);
                    Box(frame, "RearGrandstandLip", new Vector3(side * (halfW * 0.5f + 1.5f), 0.52f + tier * 0.42f, halfH + 1.25f + tier * 0.7f),
                        new Vector3(halfW + 0.65f, 0.05f, 0.12f), edge);
                }

                var towerPosition = new Vector3(side * (halfW + 2.2f), 0f, halfH + 2.7f);
                Box(frame, "ChampionBastion", towerPosition + Vector3.up * 1.15f, new Vector3(1.2f, 3f, 1.2f), stone);
                Box(frame, "BastionCyanSpine", towerPosition + new Vector3(0f, 1.3f, -0.62f), new Vector3(0.1f, 2.45f, 0.06f), cyan);
                Box(frame, "BastionCrown", towerPosition + Vector3.up * 2.7f, new Vector3(1.45f, 0.22f, 1.45f), gold);
                MeshPart(frame, "FacetedSpire", PrimitiveMeshFactory.Cone(6, 0.8f, 0.18f, 1.25f), towerPosition + Vector3.up * 2.82f, Vector3.one, Quaternion.identity, dark);
                Box(frame, "ElementBanner", towerPosition + new Vector3(side * 0.95f, 1.65f, -0.25f), new Vector3(0.62f, 1.5f, 0.08f), side < 0 ? violet : ember);
                Box(frame, "BannerCrest", towerPosition + new Vector3(side * 0.95f, 1.7f, -0.31f), new Vector3(0.23f, 0.23f, 0.04f), gold).localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            float gateHalfWidth = Mathf.Clamp(halfW * 0.65f, 1.6f, 3f);
            float goalZ = halfH + 2.8f;
            Box(frame, "ChampionDais", new Vector3(0f, -0.05f, goalZ), new Vector3(gateHalfWidth * 2f + 1.4f, 0.6f, 2.1f), dark);
            Box(frame, "ChampionStep", new Vector3(0f, -0.25f, goalZ - 1.25f), new Vector3(gateHalfWidth * 2f + 0.8f, 0.2f, 0.5f), gold);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(frame, "GatePillar", new Vector3(side * gateHalfWidth, 1.95f, goalZ + 0.4f), new Vector3(0.6f, 3.9f, 0.75f), dark);
                Box(frame, "GateGoldReveal", new Vector3(side * gateHalfWidth, 1.95f, goalZ - 0.01f), new Vector3(0.12f, 3.9f, 0.08f), gold);
                Box(frame, "GateShoulder", new Vector3(side * (gateHalfWidth - 0.24f), 3.8f, goalZ + 0.4f), new Vector3(0.9f, 0.35f, 0.85f), gold).localRotation = Quaternion.Euler(0f, 0f, side * 25f);
            }
            Box(frame, "ChampionLintel", new Vector3(0f, 4.14f, goalZ + 0.4f), new Vector3(gateHalfWidth * 2f + 0.7f, 0.42f, 0.9f), stone);
            Box(frame, "LintelCyanInlay", new Vector3(0f, 4.16f, goalZ - 0.07f), new Vector3(gateHalfWidth * 2f + 0.35f, 0.08f, 0.04f), cyan);
            MeshPart(frame, "ChampionHalo", PrimitiveMeshFactory.Torus(1.16f, 0.065f, 32, 6), new Vector3(0f, 2.4f, goalZ + 0.5f), Vector3.one, Quaternion.Euler(90f, 0f, 0f), cyan);
            Box(frame, "TrophyPlinth", new Vector3(0f, 0.62f, goalZ), new Vector3(1.05f, 0.7f, 0.9f), stone);
            Box(frame, "TrophyFoot", new Vector3(0f, 1.05f, goalZ), new Vector3(0.8f, 0.16f, 0.7f), gold);
            MeshPart(frame, "TrophyStem", PrimitiveMeshFactory.Cone(8, 0.16f, 0.12f, 0.62f), new Vector3(0f, 1.12f, goalZ), Vector3.one, Quaternion.identity, gold);
            MeshPart(frame, "ChampionCup", PrimitiveMeshFactory.Cone(10, 0.2f, 0.63f, 0.72f), new Vector3(0f, 1.68f, goalZ), Vector3.one, Quaternion.identity, gold);
            MeshPart(frame, "CupRim", PrimitiveMeshFactory.Torus(0.62f, 0.06f, 20, 6), new Vector3(0f, 2.4f, goalZ), Vector3.one, Quaternion.identity, gold);
            for (int side = -1; side <= 1; side += 2)
                MeshPart(frame, "TrophyHandle", PrimitiveMeshFactory.Torus(0.32f, 0.065f, 16, 6), new Vector3(side * 0.55f, 2.03f, goalZ), new Vector3(0.85f, 1f, 1.15f), Quaternion.Euler(90f, 0f, 0f), gold);

            StaticBatchingUtility.Combine(stadium);

            Material[] elements = { cyan, violet, gold, ember };
            for (int i = 0; i < 4; i++)
            {
                float side = i < 2 ? -1f : 1f;
                float offset = i % 2;
                var crystal = new GameObject("FloatingElementCrystal");
                crystal.transform.SetParent(root.transform, false);
                crystal.transform.localPosition = new Vector3(side * (halfW + 0.6f + offset * 1.8f), 3.3f + offset * 1.3f, halfH + 4.2f);
                MeshPart(crystal.transform, "CrystalUpper", PrimitiveMeshFactory.Cone(5, 0.38f, 0f, 0.9f), Vector3.zero, Vector3.one, Quaternion.identity, elements[i]);
                MeshPart(crystal.transform, "CrystalLower", PrimitiveMeshFactory.Cone(5, 0.38f, 0f, 0.6f), Vector3.zero, Vector3.one, Quaternion.Euler(180f, 0f, 0f), elements[i]);
                MeshPart(crystal.transform, "CrystalOrbit", PrimitiveMeshFactory.Torus(0.58f, 0.025f, 16, 4), Vector3.zero, Vector3.one, Quaternion.Euler(15f, 0f, 20f), gold);
                crystal.AddComponent<ColosseumCrystalFloat>().Phase = i * 1.7f;
            }
            return root;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return part.transform;
        }

        private static void MeshPart(Transform parent, string name, Mesh mesh, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    public class ColosseumCrystalFloat : MonoBehaviour
    {
        public float Phase;
        private Vector3 _position;

        private void Awake()
        {
            _position = transform.localPosition;
        }

        private void Update()
        {
            transform.localPosition = _position + Vector3.up * (Mathf.Sin(Time.time * 0.7f + Phase) * 0.12f);
            transform.localRotation = Quaternion.Euler(0f, Time.time * 12f + Phase * 40f, 0f);
        }
    }
}
