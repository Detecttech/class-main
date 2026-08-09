using QuizBattle.Characters;

namespace QuizBattle.GameState.MockEngine
{
    /// Mirrors server/src/matchEngine/CombatResolver.ts — keep the two in sync.
    public static class CombatResolver
    {
        public static readonly CharacterConfig BasicAttack = new CharacterConfig
        {
            abilityId = "basic_strike",
            abilityType = AbilityType.Active,
            targeting = AbilityTargeting.Melee,
            range = 1,
            baseDamage = 15,
            vfxTag = "vfx_basic_strike",
        };

        /// The attack a character uses when they consume an attack_choice reward.
        public static CharacterConfig AttackFor(string characterId)
        {
            var character = CharacterCatalog.Get(characterId);
            return character.abilityType == AbilityType.Active ? character : BasicAttack;
        }

        public struct AttackOutcome
        {
            public int damage;
            public int targetHpAfter;
            public bool eliminated;
            public int healedAttacker;
            public string vfxTag;
        }

        public static AttackOutcome ResolveAttack(PlayerState attacker, PlayerState target, CharacterConfig ability)
        {
            int damage = ability.baseDamage;

            var targetCharacter = CharacterCatalog.Get(target.characterId);
            if (targetCharacter.abilityType == AbilityType.Passive && targetCharacter.damageReductionPct > 0)
            {
                damage = UnityEngine.Mathf.RoundToInt(damage * (1f - targetCharacter.damageReductionPct / 100f));
            }

            target.hp = UnityEngine.Mathf.Max(0, target.hp - damage);
            bool eliminated = target.hp == 0;
            if (eliminated) target.alive = false;

            if (ability.dotDamage > 0 && ability.dotRounds > 0 && !eliminated)
            {
                target.pendingDot = new PendingDot { damage = ability.dotDamage, remainingRounds = ability.dotRounds };
            }

            int healedAttacker = 0;
            if (ability.lifestealPct > 0)
            {
                healedAttacker = UnityEngine.Mathf.RoundToInt(damage * (ability.lifestealPct / 100f));
                attacker.hp = UnityEngine.Mathf.Min(attacker.maxHp, attacker.hp + healedAttacker);
            }

            return new AttackOutcome
            {
                damage = damage,
                targetHpAfter = target.hp,
                eliminated = eliminated,
                healedAttacker = healedAttacker,
                vfxTag = ability.vfxTag,
            };
        }

        /// Applies any burn/DoT ticks queued on a player (e.g. from Blaze's Fireball).
        public static int TickDot(PlayerState player)
        {
            if (player.pendingDot == null) return 0;
            int dmg = player.pendingDot.damage;
            player.hp = UnityEngine.Mathf.Max(0, player.hp - dmg);
            if (player.hp == 0) player.alive = false;
            player.pendingDot.remainingRounds -= 1;
            if (player.pendingDot.remainingRounds <= 0) player.pendingDot = null;
            return dmg;
        }
    }
}
