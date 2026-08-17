using QuizBattle.Arena.Visuals;
using UnityEngine;

namespace QuizBattle.Arena
{
    /// Builds a colorful checkerboard grid from primitives at runtime — no prefab/material
    /// assets required, so it works whether or not real art has been imported yet.
    public class GridController : MonoBehaviour
    {
        public float tileSize = 1f;
        public Color colorA = QuizBattlePalette.GrassLight;
        public Color colorB = QuizBattlePalette.GrassDark;
        public Color zoneColor = QuizBattlePalette.ZoneGold;

        // Inspector-assignable; falls back to Resources so headless/editor demo runners
        // that build a GridController with no scene wiring still get a textured floor.
        public Texture2D floorTexture;
        private static readonly Vector4 TileTiling = new Vector4(0.9f, 0.9f, 0f, 0f);

        private Texture2D _resolvedFloorTexture;
        private bool _floorTextureResolved;

        private GameObject[,] _tiles;
        private int _width;
        private int _height;

        /// goalRow is the finish-line row (grid.height - 1) — every tile in it is
        /// highlighted so the race's target is obvious at a glance.
        public void BuildGrid(int width, int height, int goalRow)
        {
            ClearGrid();
            _width = width;
            _height = height;
            _tiles = new GameObject[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isGoal = y == goalRow;
                    var color = isGoal ? zoneColor : ((x + y) % 2 == 0 ? colorA : colorB);
                    _tiles[x, y] = CreateTile(x, y, color);
                    if (isGoal) CreateZoneGlow(x, y, isLeftEdge: x == 0, isRightEdge: x == width - 1);
                }
            }

            CreateCurbBorder(width, height);
            CreatePlinth(width, height);
            ArenaColosseumBuilder.Build(transform, width, height, tileSize);
        }

        private GameObject CreateTile(int x, int y, Color color)
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = $"Tile_{x}_{y}";
            tile.transform.SetParent(transform, false);
            tile.transform.localPosition = new Vector3(x * tileSize, -0.5f, y * tileSize);
            tile.transform.localScale = new Vector3(tileSize * 0.96f, 1f, tileSize * 0.96f);
            Destroy(tile.GetComponent<Collider>());

            var renderer = tile.GetComponent<Renderer>();
            // Shared/cached — tiles are never mutated after creation, unlike CharacterToken's body.
            // Tiles get a thinner outline than characters (TileStyle) so the checkerboard's
            // seams don't read as busy compared to the chunkier character outlines.
            var tex = ResolveFloorTexture();
            renderer.sharedMaterial = tex != null
                ? ToonMaterialFactory.Toon(color, TileStyle, tex, TileTiling)
                : ToonMaterialFactory.Toon(color, TileStyle);

            return tile;
        }

        private Texture2D ResolveFloorTexture()
        {
            if (_floorTextureResolved) return _resolvedFloorTexture;
            _resolvedFloorTexture = floorTexture;
            _floorTextureResolved = true;
            return _resolvedFloorTexture;
        }

        private static ToonStyle TileStyle
        {
            get
            {
                var style = ToonStyle.Default;
                style.OutlineWidth = 0.8f;
                style.RimIntensity = 0.45f;
                return style;
            }
        }

        private void CreateZoneGlow(int x, int y, bool isLeftEdge, bool isRightEdge)
        {
            // A flattened cylinder pulsing softly above the goal tile
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = $"ZoneGlow_{x}_{y}";
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(x * tileSize, 0.52f, y * tileSize);
            glow.transform.localScale = new Vector3(tileSize * 0.92f, 0.02f, tileSize * 0.92f);
            Destroy(glow.GetComponent<Collider>());
            glow.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.Glow(zoneColor, intensity: 1.2f, softEdge: 0.35f, pulseSpeed: 2.5f, pulseAmount: 0.35f);

            // Goal Finish Posts on the left and right ends of the finish line
            if (isLeftEdge || isRightEdge)
            {
                CreateGoalPost(x, y, isLeftEdge);
            }
        }

        private void CreateGoalPost(int x, int y, bool isLeft)
        {
            var post = new GameObject(isLeft ? "GoalPost_Left" : "GoalPost_Right");
            post.transform.SetParent(transform, false);
            float dir = isLeft ? -0.55f : 0.55f;
            post.transform.localPosition = new Vector3(x * tileSize + dir * tileSize, 0.5f, y * tileSize);

            var pillarMat = ToonMaterialFactory.Toon(QuizBattlePalette.StoneBorder, ToonStyle.Default);
            var goldMat = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, ToonStyle.Default);

            // Pillar
            var col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = "Pillar";
            col.transform.SetParent(post.transform, false);
            col.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            col.transform.localScale = new Vector3(0.32f, 0.6f, 0.32f);
            Destroy(col.GetComponent<Collider>());
            col.GetComponent<Renderer>().sharedMaterial = pillarMat;

            // Golden crown cap
            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "CrownCap";
            crown.transform.SetParent(post.transform, false);
            crown.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            crown.transform.localScale = Vector3.one * 0.35f;
            Destroy(crown.GetComponent<Collider>());
            crown.GetComponent<Renderer>().sharedMaterial = goldMat;
        }

        private void CreateCurbBorder(int width, int height)
        {
            var curbContainer = new GameObject("CurbBorders");
            curbContainer.transform.SetParent(transform, false);

            var curbMat = ToonMaterialFactory.Toon(QuizBattlePalette.StoneBorder, ToonStyle.Default);
            var studMat = ToonMaterialFactory.Toon(QuizBattlePalette.GoldTrim, ToonStyle.Default);

            float totalW = width * tileSize;
            float totalH = height * tileSize;
            float cx = (width - 1) * tileSize * 0.5f;
            float cz = (height - 1) * tileSize * 0.5f;
            float curbThick = 0.22f;
            float curbH = 0.18f;

            // North, South, East, West borders
            CreateCurbSegment(curbContainer.transform, new Vector3(cx, 0.04f, cz + totalH * 0.5f + curbThick * 0.5f), new Vector3(totalW + curbThick * 2f, curbH, curbThick), curbMat);
            CreateCurbSegment(curbContainer.transform, new Vector3(cx, 0.04f, cz - totalH * 0.5f - curbThick * 0.5f), new Vector3(totalW + curbThick * 2f, curbH, curbThick), curbMat);
            CreateCurbSegment(curbContainer.transform, new Vector3(cx + totalW * 0.5f + curbThick * 0.5f, 0.04f, cz), new Vector3(curbThick, curbH, totalH), curbMat);
            CreateCurbSegment(curbContainer.transform, new Vector3(cx - totalW * 0.5f - curbThick * 0.5f, 0.04f, cz), new Vector3(curbThick, curbH, totalH), curbMat);

            // 4 Golden corner studs
            Vector3[] cornerStuds = {
                new Vector3(cx - totalW * 0.5f - curbThick * 0.5f, 0.12f, cz - totalH * 0.5f - curbThick * 0.5f),
                new Vector3(cx + totalW * 0.5f + curbThick * 0.5f, 0.12f, cz - totalH * 0.5f - curbThick * 0.5f),
                new Vector3(cx - totalW * 0.5f - curbThick * 0.5f, 0.12f, cz + totalH * 0.5f + curbThick * 0.5f),
                new Vector3(cx + totalW * 0.5f + curbThick * 0.5f, 0.12f, cz + totalH * 0.5f + curbThick * 0.5f),
            };

            foreach (var sPos in cornerStuds)
            {
                var stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stud.name = "CornerStud";
                stud.transform.SetParent(curbContainer.transform, false);
                stud.transform.localPosition = sPos;
                stud.transform.localScale = new Vector3(0.24f, 0.08f, 0.24f);
                Destroy(stud.GetComponent<Collider>());
                stud.GetComponent<Renderer>().sharedMaterial = studMat;
            }
        }

        private static void CreateCurbSegment(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            curb.name = "Curb";
            curb.transform.SetParent(parent, false);
            curb.transform.localPosition = pos;
            curb.transform.localScale = size;
            Destroy(curb.GetComponent<Collider>());
            curb.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private void CreatePlinth(int width, int height)
        {
            var plinthContainer = new GameObject("Plinth_Foundation");
            plinthContainer.transform.SetParent(transform, false);
            float cx = (width - 1) * tileSize / 2f;
            float cz = (height - 1) * tileSize / 2f;

            var upperPlinthStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.PlinthShadowTint,
                RimColor = Color.white,
                RimIntensity = 0.35f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.6f,
                OutlineEnabled = true,
            };
            var lowerPlinthStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.PlinthShadowTint,
                RimColor = Color.white,
                RimIntensity = 0.2f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.4f,
                OutlineEnabled = true,
            };

            var upperMat = ToonMaterialFactory.Toon(QuizBattlePalette.StoneBorder, upperPlinthStyle);
            var lowerMat = ToonMaterialFactory.Toon(QuizBattlePalette.PlinthColor, lowerPlinthStyle);

            // Tier 1 (Upper Bevel Stone)
            var plinth1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth1.name = "Plinth_Tier1";
            plinth1.transform.SetParent(plinthContainer.transform, false);
            plinth1.transform.localPosition = new Vector3(cx, -1.05f, cz);
            plinth1.transform.localScale = new Vector3(width * tileSize + 0.6f, 0.45f, height * tileSize + 0.6f);
            Destroy(plinth1.GetComponent<Collider>());
            plinth1.GetComponent<Renderer>().sharedMaterial = upperMat;

            // Tier 2 (Deep Foundation Plinth)
            var plinth2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth2.name = "Plinth_Tier2";
            plinth2.transform.SetParent(plinthContainer.transform, false);
            plinth2.transform.localPosition = new Vector3(cx, -1.55f, cz);
            plinth2.transform.localScale = new Vector3(width * tileSize + 1.2f, 0.65f, height * tileSize + 1.2f);
            Destroy(plinth2.GetComponent<Collider>());
            plinth2.GetComponent<Renderer>().sharedMaterial = lowerMat;
        }

        public Vector3 TileToWorldPos(int x, int y) =>
            transform.TransformPoint(new Vector3(x * tileSize, 0.51f, y * tileSize));

        public Vector3 GridCenter() =>
            transform.TransformPoint(new Vector3((_width - 1) * tileSize / 2f, 0f, (_height - 1) * tileSize / 2f));

        public void ClearGrid()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
