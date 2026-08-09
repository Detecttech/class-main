using System.Collections.Generic;
using QuizBattle.Characters;

namespace QuizBattle.GameState.MockEngine
{
    /// Plain-data mirror of a CharacterDefinitionSO, decoupled from UnityEngine.Object so
    /// the match engine core stays testable without touching the asset database.
    public class CharacterConfig
    {
        public string characterId;
        public string displayName;
        public int maxHp;
        public int moveRange;
        public string abilityId;
        public AbilityType abilityType;
        public AbilityTargeting targeting;
        public int range;
        public int baseDamage;
        public int dotDamage;
        public int dotRounds;
        public int damageReductionPct;
        public int bonusMoveSteps;
        public int lifestealPct;
        public string vfxTag;

        public static CharacterConfig FromDefinition(CharacterDefinitionSO def) => new CharacterConfig
        {
            characterId = def.characterId,
            displayName = def.displayName,
            maxHp = def.maxHp,
            moveRange = def.moveRange,
            abilityId = def.abilityId,
            abilityType = def.abilityType,
            targeting = def.targeting,
            range = def.range,
            baseDamage = def.baseDamage,
            dotDamage = def.dotDamage,
            dotRounds = def.dotRounds,
            damageReductionPct = def.damageReductionPct,
            bonusMoveSteps = def.bonusMoveSteps,
            lifestealPct = def.lifestealPct,
            vfxTag = def.vfxTag,
        };
    }

    /// In-memory catalog the match engine reads from — populated once at startup (or in
    /// test setup) via Register/RegisterAll rather than loading assets itself.
    public static class CharacterCatalog
    {
        private static readonly Dictionary<string, CharacterConfig> _byId = new Dictionary<string, CharacterConfig>();

        public static void Register(CharacterConfig config) => _byId[config.characterId] = config;

        public static void RegisterAll(IEnumerable<CharacterConfig> configs)
        {
            foreach (var c in configs) Register(c);
        }

        public static void Clear() => _byId.Clear();

        public static CharacterConfig Get(string characterId)
        {
            if (!_byId.TryGetValue(characterId, out var config))
                throw new System.Exception($"Unknown character id: {characterId}");
            return config;
        }

        public static bool Contains(string characterId) => _byId.ContainsKey(characterId);
    }
}
