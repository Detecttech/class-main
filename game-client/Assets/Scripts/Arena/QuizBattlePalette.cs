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

        // Arena floor — warm sand/terracotta checkerboard instead of cool lavender.
        public static readonly Color WarmTileLight = new Color(0.86f, 0.72f, 0.52f);
        public static readonly Color WarmTileDark = new Color(0.70f, 0.52f, 0.34f);
        public static readonly Color PlinthColor = new Color(0.30f, 0.20f, 0.14f);
        public static readonly Color PlinthShadowTint = new Color(0.30f, 0.18f, 0.14f);

        // UI banner panels — deep purple/blue, like CR's menu chrome.
        public static readonly Color PanelDeep = new Color(0.16f, 0.14f, 0.32f);
        public static readonly Color PanelFill = new Color(0.30f, 0.24f, 0.52f);
        public static readonly Color PanelHighlighted = new Color(0.42f, 0.34f, 0.68f);
        public static readonly Color PanelPressed = new Color(0.20f, 0.16f, 0.38f);
        public static readonly Color CreamText = new Color(0.97f, 0.92f, 0.80f);
        public static readonly Color ParchmentField = new Color(0.96f, 0.90f, 0.78f);

        // Shared outline/shadow tone for the toon shader.
        public static readonly Color OutlineColor = new Color(0.04f, 0.03f, 0.05f);
        public static readonly Color ShadowTint = new Color(0.32f, 0.22f, 0.40f);
    }
}
