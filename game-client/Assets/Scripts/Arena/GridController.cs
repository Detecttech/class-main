using QuizBattle.Arena.Visuals;
using UnityEngine;

namespace QuizBattle.Arena
{
    /// Builds a colorful checkerboard grid from primitives at runtime — no prefab/material
    /// assets required, so it works whether or not real art has been imported yet.
    public class GridController : MonoBehaviour
    {
        public float tileSize = 1f;
        public Color colorA = QuizBattlePalette.WarmTileLight;
        public Color colorB = QuizBattlePalette.WarmTileDark;
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
                    if (isGoal) CreateZoneGlow(x, y);
                }
            }

            CreatePlinth(width, height);
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
            _resolvedFloorTexture = floorTexture != null ? floorTexture : Resources.Load<Texture2D>("Textures/StoneFloor");
            _floorTextureResolved = true;
            return _resolvedFloorTexture;
        }

        private static ToonStyle TileStyle
        {
            get
            {
                var style = ToonStyle.Default;
                style.OutlineWidth = 0.8f;
                return style;
            }
        }

        private void CreateZoneGlow(int x, int y)
        {
            // A flattened cylinder (not a Quad — its cap always faces up regardless of
            // rotation, avoiding guesswork about Unity's default Quad normal direction)
            // pulsing softly above the tile.
            var glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = $"ZoneGlow_{x}_{y}";
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(x * tileSize, 0.52f, y * tileSize);
            glow.transform.localScale = new Vector3(tileSize * 0.88f, 0.02f, tileSize * 0.88f);
            Destroy(glow.GetComponent<Collider>());
            glow.GetComponent<Renderer>().sharedMaterial =
                ToonMaterialFactory.Glow(zoneColor, intensity: 0.8f, softEdge: 0.4f, pulseSpeed: 2.2f, pulseAmount: 0.35f);
        }

        private void CreatePlinth(int width, int height)
        {
            var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plinth.name = "Plinth";
            plinth.transform.SetParent(transform, false);
            float cx = (width - 1) * tileSize / 2f;
            float cz = (height - 1) * tileSize / 2f;
            plinth.transform.localPosition = new Vector3(cx, -1.25f, cz);
            plinth.transform.localScale = new Vector3(width * tileSize + 0.3f, 0.7f, height * tileSize + 0.3f);
            Destroy(plinth.GetComponent<Collider>());

            var plinthStyle = new ToonStyle
            {
                ShadowTint = QuizBattlePalette.PlinthShadowTint,
                RimColor = Color.white,
                RimIntensity = 0.3f,
                EmissionColor = Color.black,
                EmissionIntensity = 0f,
                OutlineColor = QuizBattlePalette.OutlineColor,
                OutlineWidth = 1.8f,
                OutlineEnabled = true,
            };
            plinth.GetComponent<Renderer>().sharedMaterial = ToonMaterialFactory.Toon(QuizBattlePalette.PlinthColor, plinthStyle);
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
