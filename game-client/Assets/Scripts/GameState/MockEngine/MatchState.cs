using System.Collections.Generic;

namespace QuizBattle.GameState.MockEngine
{
    public struct GridPos
    {
        public int x;
        public int y;

        public GridPos(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override bool Equals(object obj) => obj is GridPos p && p.x == x && p.y == y;
        public override int GetHashCode() => (x, y).GetHashCode();
    }

    public enum RewardType
    {
        AttackChoice,
        Freeze,
        BonusMove,
    }

    public class PendingReward
    {
        public string rewardId;
        public RewardType type;
        public int expiresAtQuestion; // relative to the owning player's own questionsAnswered count
    }

    public class PendingDot
    {
        public int damage;
        public int remainingRounds;
    }

    public class ActiveQuestion
    {
        public int questionId;
        public int correctIndex;
        public int questionNumber; // this player's own Nth question, not a match-wide round
    }

    public class PlayerState
    {
        public int playerId;
        public string name;
        public string characterId;
        public string team;
        public int hp;
        public int maxHp;
        public GridPos pos;
        public bool alive = true;
        public int consecutiveCorrect;
        public RewardType? lastRewardType;
        public PendingReward pendingReward;
        public PendingDot pendingDot;
        public int totalCorrectAnswers;
        public int maxStreak;
        public int questionsAnswered; // drives reward expiry + the match-length forced-decision cap
        public ActiveQuestion currentQuestion; // independent per player, not shared
        public bool goalReached;
        public bool frozen; // consumed on this player's next correct answer — that answer won't advance them
        public int? lastTargetedPlayerId; // for the "can't attack/freeze the same player twice in a row" rule
    }

    public class AnswerRecord
    {
        public int choiceIndex;
        public bool correct;
    }

    public enum WinReason
    {
        Hp,
        Goal,
        Progress,
    }

    public class MatchResult
    {
        public object winnerId; // int (FFA playerId) or string (team name)
        public WinReason reason;
    }

    public enum MatchMode
    {
        Ffa,
        Teams,
    }

    public enum MatchStatus
    {
        Lobby,
        Active,
        Completed,
    }

    public class MatchState
    {
        public const int DefaultGridWidth = 8;
        public const int DefaultGridHeight = 6;
        public const int RewardExpiryQuestions = 4;
        public const int DefaultBonusMoveSteps = 2;

        // Mirrors server MatchState.ts's computeGridHeight — steps-to-win tracks the
        // question bank size, clamped to a sane range. Not used by the local mock demo
        // today (it has no fixed bank size), but kept in lockstep for API parity.
        private const int MinGoalSteps = 4;
        private const int MaxGoalSteps = 30;

        public static int ComputeGridHeight(int questionCount)
        {
            int steps = System.Math.Min(MaxGoalSteps, System.Math.Max(MinGoalSteps, questionCount));
            return steps + 1;
        }

        public int matchId;
        public MatchMode mode;
        public MatchStatus status = MatchStatus.Lobby;
        public int maxRounds; // max questions any one player answers before a forced progress tiebreak
        public int gridWidth = DefaultGridWidth;
        public int gridHeight = DefaultGridHeight;
        public Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
        public MatchResult result;

        public MatchState(int matchId, MatchMode mode, int maxRounds = 20, int? gridHeight = null)
        {
            this.matchId = matchId;
            this.mode = mode;
            this.maxRounds = maxRounds;
            if (gridHeight.HasValue) this.gridHeight = gridHeight.Value;
        }
    }
}
