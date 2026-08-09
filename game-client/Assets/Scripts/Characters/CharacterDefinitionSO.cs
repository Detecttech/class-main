using UnityEngine;

namespace QuizBattle.Characters
{
    /// Mirrors shared/characters.json — keep both in sync by hand for now (v1 scope);
    /// a build-time generator that reads the shared JSON is a reasonable later addition.
    [CreateAssetMenu(fileName = "CharacterDef", menuName = "QuizBattle/Character Definition")]
    public class CharacterDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string characterId;
        public string displayName;
        public Color placeholderColor = Color.white;
        public CharacterArchetype archetype;
        public Color accentColor = Color.white;
        public Color emissionColor = Color.black;

        [Header("Base Stats")]
        public int maxHp = 100;
        public int moveRange = 1;

        [Header("Ability")]
        public string abilityId;
        public string abilityName;
        public AbilityType abilityType;
        [TextArea] public string abilityDescription;
        public AbilityTargeting targeting;
        public int range;
        public int baseDamage;
        public int dotDamage;
        public int dotRounds;
        public int damageReductionPct;
        public int bonusMoveSteps;
        public int lifestealPct;
        public string vfxTag;

        [Header("Unlock")]
        public bool defaultUnlocked;
        public int xpThreshold;
    }
}
