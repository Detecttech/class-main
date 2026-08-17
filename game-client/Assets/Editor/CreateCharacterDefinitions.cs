using UnityEditor;
using UnityEngine;
using QuizBattle.Characters;

public static class CreateCharacterDefinitions
{
    [MenuItem("Tools/Scaffold/Create Character Definitions")]
    public static void Run()
    {
        Create(new CharacterDefinitionSO
        {
            characterId = "blaze",
            displayName = "Blaze",
            placeholderColor = new Color(1f, 0.45f, 0.1f),
            archetype = CharacterArchetype.Fire,
            accentColor = new Color(1f, 0.75f, 0.2f),
            emissionColor = new Color(1f, 0.55f, 0.1f),
            maxHp = 45,
            moveRange = 1,
            abilityId = "fireball",
            abilityName = "Fireball",
            abilityType = AbilityType.Active,
            abilityDescription = "Ranged single-target attack with a small burn effect.",
            targeting = AbilityTargeting.Ranged,
            range = 3,
            baseDamage = 20,
            dotDamage = 5,
            dotRounds = 1,
            vfxTag = "vfx_fireball",
            defaultUnlocked = true,
        });

        Create(new CharacterDefinitionSO
        {
            characterId = "aegis",
            displayName = "Aegis",
            placeholderColor = new Color(0.2f, 0.5f, 1f),
            archetype = CharacterArchetype.Tank,
            accentColor = new Color(0.7f, 0.78f, 0.9f),
            emissionColor = Color.black,
            maxHp = 55,
            moveRange = 1,
            abilityId = "bulwark",
            abilityName = "Bulwark",
            abilityType = AbilityType.Passive,
            abilityDescription = "Takes 25% reduced damage from all attacks, always on.",
            damageReductionPct = 25,
            vfxTag = "vfx_shield_shimmer",
            defaultUnlocked = true,
        });

        Create(new CharacterDefinitionSO
        {
            characterId = "zephyr",
            displayName = "Zephyr",
            placeholderColor = new Color(0.25f, 0.85f, 0.4f),
            archetype = CharacterArchetype.Wind,
            accentColor = new Color(0.7f, 1f, 0.8f),
            emissionColor = new Color(0.4f, 1f, 0.6f),
            maxHp = 50,
            moveRange = 1,
            abilityId = "windstep",
            abilityName = "Windstep",
            abilityType = AbilityType.Passive,
            abilityDescription = "Bonus-move rewards grant +3 steps instead of +2.",
            bonusMoveSteps = 3,
            vfxTag = "vfx_wind_trail",
            defaultUnlocked = false,
            xpThreshold = 300,
        });

        Create(new CharacterDefinitionSO
        {
            characterId = "vera",
            displayName = "Vera",
            placeholderColor = new Color(0.7f, 0.3f, 0.9f),
            archetype = CharacterArchetype.Arcane,
            accentColor = new Color(0.9f, 0.6f, 1f),
            emissionColor = new Color(0.85f, 0.35f, 1f),
            maxHp = 50,
            moveRange = 1,
            abilityId = "life_drain",
            abilityName = "Life Drain",
            abilityType = AbilityType.Active,
            abilityDescription = "Melee/short-range attack that heals Vera for 50% of damage dealt.",
            targeting = AbilityTargeting.Melee,
            range = 1,
            baseDamage = 16,
            lifestealPct = 50,
            vfxTag = "vfx_life_drain",
            defaultUnlocked = false,
            xpThreshold = 600,
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CreateCharacterDefinitions] Created 4 character definition assets.");
    }

    private static void Create(CharacterDefinitionSO template)
    {
        // Lives under Resources so CharacterCatalogLoader can Resources.LoadAll it at
        // runtime on-device (matches the plan's "start with plain Resources for v1").
        var path = $"Assets/Resources/Characters/{template.displayName}Def.asset";
        var oldPath = $"Assets/ScriptableObjects/Characters/{template.displayName}Def.asset";
        if (AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(oldPath) != null && AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path) == null)
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/Characters");
            AssetDatabase.MoveAsset(oldPath, path);
        }

        var existing = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
        var asset = existing != null ? existing : ScriptableObject.CreateInstance<CharacterDefinitionSO>();

        asset.characterId = template.characterId;
        asset.displayName = template.displayName;
        asset.placeholderColor = template.placeholderColor;
        asset.archetype = template.archetype;
        asset.accentColor = template.accentColor;
        asset.emissionColor = template.emissionColor;
        asset.maxHp = template.maxHp;
        asset.moveRange = template.moveRange;
        asset.abilityId = template.abilityId;
        asset.abilityName = template.abilityName;
        asset.abilityType = template.abilityType;
        asset.abilityDescription = template.abilityDescription;
        asset.targeting = template.targeting;
        asset.range = template.range;
        asset.baseDamage = template.baseDamage;
        asset.dotDamage = template.dotDamage;
        asset.dotRounds = template.dotRounds;
        asset.damageReductionPct = template.damageReductionPct;
        asset.bonusMoveSteps = template.bonusMoveSteps;
        asset.lifestealPct = template.lifestealPct;
        asset.vfxTag = template.vfxTag;
        asset.defaultUnlocked = template.defaultUnlocked;
        asset.xpThreshold = template.xpThreshold;

        if (existing == null)
        {
            AssetDatabase.CreateAsset(asset, path);
        }
        else
        {
            EditorUtility.SetDirty(asset);
        }
    }
}
