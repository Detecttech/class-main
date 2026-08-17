using UnityEngine;

namespace QuizBattle.Arena
{
    /// Single source of truth for the game's Clash-Royale-inspired warm/saturated
    /// palette — coordinates colors that previously lived as ad hoc literals scattered
    /// across GridController, UiFactory, ArenaEnvironment, and ToonMaterialFactory.
    public static class QuizBattlePalette
    {
        // Gold accents — reserved for zones, borders, and premium/reward UI.
        public static readonly Color GoldTrim = new Color(1.00f, 0.82f, 0.25f);
        public static readonly Color GoldTrimDark = new Color(0.72f, 0.52f, 0.10f);
        public static readonly Color ZoneGold = new Color(1.00f, 0.78f, 0.12f);

        // Arena floor — vibrant Clash Royale lush grass lawn checkerboard.
        public static readonly Color GrassLight = new Color(0.38f, 0.76f, 0.22f);
        public static readonly Color GrassDark = new Color(0.28f, 0.64f, 0.15f);
        public static readonly Color GrassHighlight = new Color(0.48f, 0.84f, 0.28f);

        // Legacy warm tile fallbacks for sand/terracotta themes.
        public static readonly Color WarmTileLight = new Color(0.86f, 0.72f, 0.52f);
        public static readonly Color WarmTileDark = new Color(0.70f, 0.52f, 0.34f);

        // Arena Colosseum stone, wood & foundation.
        public static readonly Color StoneBorder = new Color(0.68f, 0.72f, 0.78f);
        public static readonly Color StoneWall = new Color(0.50f, 0.54f, 0.62f);
        public static readonly Color StoneDark = new Color(0.34f, 0.38f, 0.46f);
        public static readonly Color PlinthColor = new Color(0.28f, 0.30f, 0.38f);
        public static readonly Color PlinthShadowTint = new Color(0.18f, 0.16f, 0.26f);

        public static readonly Color WoodPlank = new Color(0.58f, 0.38f, 0.20f);
        public static readonly Color WoodDark = new Color(0.38f, 0.24f, 0.12f);
        public static readonly Color RoofTilesRed = new Color(0.82f, 0.24f, 0.20f);
        public static readonly Color RoofTilesBlue = new Color(0.20f, 0.44f, 0.84f);

        // Royal banners & heraldry.
        public static readonly Color BannerBlue = new Color(0.18f, 0.46f, 0.90f);
        public static readonly Color BannerRed = new Color(0.86f, 0.22f, 0.22f);
        public static readonly Color BannerGoldTrim = new Color(1.00f, 0.82f, 0.20f);

        // Braziers & Fire.
        public static readonly Color BrazierIron = new Color(0.22f, 0.20f, 0.24f);
        public static readonly Color BrazierGold = new Color(0.95f, 0.75f, 0.18f);
        public static readonly Color FireGlow = new Color(1.00f, 0.60f, 0.10f);

        // Foliage & Props.
        public static readonly Color FoliageGreen = new Color(0.22f, 0.60f, 0.18f);
        public static readonly Color FoliageDark = new Color(0.14f, 0.44f, 0.12f);
        public static readonly Color WaterBlue = new Color(0.24f, 0.68f, 0.94f);
        public static readonly Color WaterFoam = new Color(0.85f, 0.96f, 1.00f);

        // Sky & Clouds.
        public static readonly Color SkyZenith = new Color(0.28f, 0.58f, 0.92f);
        public static readonly Color SkyHorizon = new Color(0.68f, 0.86f, 0.98f);
        public static readonly Color CloudWhite = new Color(0.96f, 0.98f, 1.00f);
        public static readonly Color CloudShadow = new Color(0.78f, 0.84f, 0.94f);

        // UI banner panels — deep purple/blue, like CR's menu chrome.
        public static readonly Color PanelDeep = new Color(0.16f, 0.14f, 0.32f);
        public static readonly Color PanelFill = new Color(0.30f, 0.24f, 0.52f);
        public static readonly Color PanelHighlighted = new Color(0.42f, 0.34f, 0.68f);
        public static readonly Color PanelPressed = new Color(0.20f, 0.16f, 0.38f);
        public static readonly Color CreamText = new Color(0.97f, 0.92f, 0.80f);
        public static readonly Color ParchmentField = new Color(0.96f, 0.90f, 0.78f);

        // Shared outline/shadow tone for the toon shader.
        public static readonly Color OutlineColor = new Color(0.04f, 0.03f, 0.05f);
        public static readonly Color ShadowTint = new Color(0.24f, 0.18f, 0.36f);
    }
}
