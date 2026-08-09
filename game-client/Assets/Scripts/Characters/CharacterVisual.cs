using UnityEngine;

namespace QuizBattle.Characters
{
    /// Everything CharacterVisualBuilder needs to construct a token's silhouette,
    /// threaded through instead of adding more parallel characterId-keyed dictionaries.
    public struct CharacterVisual
    {
        public CharacterArchetype Archetype;
        public Color BaseColor;
        public Color AccentColor;
        public Color EmissionColor;
        public string VfxTag;

        public static CharacterVisual From(CharacterDefinitionSO def) => new CharacterVisual
        {
            Archetype = def.archetype,
            BaseColor = def.placeholderColor,
            AccentColor = def.accentColor,
            EmissionColor = def.emissionColor,
            VfxTag = def.vfxTag,
        };

        /// Used when only a bare color is available (e.g. a missed/legacy call site) so
        /// the token still renders as a generic capsule instead of failing to compile.
        public static CharacterVisual Fallback(Color color) => new CharacterVisual
        {
            Archetype = CharacterArchetype.Generic,
            BaseColor = color,
            AccentColor = color,
            EmissionColor = Color.black,
            VfxTag = null,
        };
    }
}
