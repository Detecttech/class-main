using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuizBattle.Networking.Protocol
{
    // Typed payloads for the WS events the server broadcasts/unicasts — mirrors
    // server/src/matchEngine/LiveMatchRegistry.ts and ws/handlers/*.ts exactly.
    // Deserialize via envelope.Payload.ToObject<T>().

    public class HelloAckPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("serverTime")] public long ServerTime;
    }

    public class GridPosPayload
    {
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
    }

    public class LobbyPlayerPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("name")] public string Name;
        [JsonProperty("characterId")] public string CharacterId;
        [JsonProperty("team")] public string Team;
        [JsonProperty("ready")] public bool Ready;
    }

    public class LobbyStatePayload
    {
        [JsonProperty("matchId")] public int MatchId;
        [JsonProperty("mode")] public string Mode;
        [JsonProperty("players")] public List<LobbyPlayerPayload> Players;
    }

    public class CharacterLockedPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("characterId")] public string CharacterId;
    }

    public class ArenaLayoutPayload
    {
        [JsonProperty("grid")] public GridSizePayload Grid;
        [JsonProperty("goalRow")] public int GoalRow;
    }

    public class GridSizePayload
    {
        [JsonProperty("width")] public int Width;
        [JsonProperty("height")] public int Height;
    }

    public class MatchPlayerPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("name")] public string Name;
        [JsonProperty("characterId")] public string CharacterId;
        [JsonProperty("team")] public string Team;
        [JsonProperty("hp")] public int Hp;
        [JsonProperty("maxHp")] public int MaxHp;
        [JsonProperty("pos")] public GridPosPayload Pos;
        [JsonProperty("alive")] public bool Alive;
    }

    public class MatchStartPayload
    {
        [JsonProperty("arenaLayout")] public ArenaLayoutPayload ArenaLayout;
        [JsonProperty("players")] public List<MatchPlayerPayload> Players;
        [JsonProperty("teams")] public bool Teams;
    }

    public class QuestionPushPayload
    {
        [JsonProperty("questionId")] public int QuestionId;
        [JsonProperty("text")] public string Text;
        [JsonProperty("choices")] public List<string> Choices;
        [JsonProperty("timeLimitMs")] public int TimeLimitMs;
        // This player's own Nth question — every player answers independently, at their
        // own pace, on their own randomly-drawn question, so there is no shared round.
        [JsonProperty("questionNumber")] public int QuestionNumber;
    }

    public class RewardOfferedPayload
    {
        [JsonProperty("rewardId")] public string RewardId;
        [JsonProperty("type")] public string Type; // "attack_choice" | "freeze" | "bonus_move"
    }

    public class AnswerResultPayload
    {
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("error")] public string Error;
        [JsonProperty("correct")] public bool Correct;
        [JsonProperty("streakCount")] public int StreakCount;
        [JsonProperty("rewardOffered")] public RewardOfferedPayload RewardOffered;
    }

    /// Broadcast to everyone whenever a player's turn resolves (answered, timed out, or
    /// consumed a bonus_move) — the replacement for the old shared/synced round_resolved
    /// and move_result events, since every player now advances independently instead of
    /// in a batch.
    public class PlayerAdvancedPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("newGridPos")] public GridPosPayload NewGridPos;
        [JsonProperty("hp")] public int Hp;
        [JsonProperty("maxHp")] public int MaxHp;
        [JsonProperty("alive")] public bool Alive;
        [JsonProperty("streak")] public int Streak;
        [JsonProperty("goalReached")] public bool GoalReached;
        [JsonProperty("frozen")] public bool Frozen;
    }

    public class AttackResultPayload
    {
        [JsonProperty("attackerId")] public int AttackerId;
        [JsonProperty("targetId")] public int TargetId;
        [JsonProperty("damage")] public int Damage;
        [JsonProperty("targetHpAfter")] public int TargetHpAfter;
        [JsonProperty("vfxTag")] public string VfxTag;
        [JsonProperty("eliminated")] public bool Eliminated;
    }

    public class PlayerEliminatedPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
    }

    /// Broadcast whenever a freeze reward is used — everyone (including the target) sees
    /// who cast it and who it landed on, mirroring how attack_result works for attacks.
    public class FreezeResultPayload
    {
        [JsonProperty("casterId")] public int CasterId;
        [JsonProperty("targetId")] public int TargetId;
    }

    public class StandingEntry
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("placement")] public int Placement;
        [JsonProperty("hp")] public int Hp;
        [JsonProperty("alive")] public bool Alive;
    }

    public class MatchEndPayload
    {
        [JsonProperty("winnerId")] public object WinnerId;
        [JsonProperty("reason")] public string Reason; // "hp" | "goal" | "progress"
        [JsonProperty("standings")] public List<StandingEntry> Standings;
    }

    public class XpAwardPayload
    {
        [JsonProperty("xpGained")] public int XpGained;
        [JsonProperty("newTotalXp")] public int NewTotalXp;
        [JsonProperty("newUnlocks")] public List<string> NewUnlocks;
    }

    public class ErrorPayload
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("message")] public string Message;
    }

    public class LiveDashboardPlayerPayload
    {
        [JsonProperty("playerId")] public int PlayerId;
        [JsonProperty("name")] public string Name;
        [JsonProperty("hp")] public int Hp;
        [JsonProperty("alive")] public bool Alive;
        [JsonProperty("streak")] public int Streak;
        [JsonProperty("pos")] public GridPosPayload Pos;
        [JsonProperty("goalReached")] public bool GoalReached;
        [JsonProperty("questionsAnswered")] public int QuestionsAnswered;
    }

    public class LiveDashboardPayload
    {
        [JsonProperty("matchId")] public int MatchId;
        [JsonProperty("status")] public string Status;
        [JsonProperty("players")] public List<LiveDashboardPlayerPayload> Players;
    }
}
