using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using QuizBattle.Networking;
using QuizBattle.Networking.Protocol;
using UnityEngine;

namespace QuizBattle.GameState
{
    public class ClientPlayerState
    {
        public int playerId;
        public string name;
        public string characterId;
        public string team;
        public int hp;
        public int maxHp;
        public Vector2Int pos;
        public bool alive = true;
        public int streak;
        public bool goalReached;
        public bool frozen;
    }

    public class ClientLobbyPlayer
    {
        public int playerId;
        public string name;
        public string characterId;
        public string team;
        public bool ready;
    }

    /// Client-side mirror of server truth (see project plan: "renderer of server state,
    /// not a decision-maker"). Every field here is set from a server broadcast; nothing
    /// is computed locally. Bind() wires it to a WsClient's MessageReceived event.
    public class MatchStateStore
    {
        public int MatchId { get; private set; }
        public string Mode { get; private set; }
        public string Status { get; private set; } = "lobby";
        // This player's own question count — every player answers independently at
        // their own pace, so there is no shared match-wide round number anymore.
        public int QuestionNumber { get; private set; }
        public int GridWidth { get; private set; } = 8;
        public int GridHeight { get; private set; } = 6;
        public int GoalRow { get; private set; } = 5;
        public Dictionary<int, ClientPlayerState> Players { get; } = new Dictionary<int, ClientPlayerState>();
        public List<ClientLobbyPlayer> LobbyPlayers { get; private set; } = new List<ClientLobbyPlayer>();
        public QuestionPushPayload CurrentQuestion { get; private set; }
        public MatchEndPayload MatchResult { get; private set; }
        public XpAwardPayload LastXpAward { get; private set; }

        public event Action<LobbyStatePayload> LobbyUpdated;
        public event Action<CharacterLockedPayload> CharacterLocked;
        public event Action<MatchStartPayload> MatchStarted;
        public event Action<QuestionPushPayload> QuestionPushed;
        public event Action<AnswerResultPayload> AnswerResultReceived;
        public event Action<PlayerAdvancedPayload> PlayerAdvanced;
        public event Action<AttackResultPayload> AttackResolved;
        public event Action<FreezeResultPayload> FreezeResolved;
        public event Action<int> PlayerEliminated;
        public event Action<MatchEndPayload> MatchEnded;
        public event Action<XpAwardPayload> XpAwarded;
        public event Action<LiveDashboardPayload> DashboardUpdated;
        public event Action<ErrorPayload> ServerError;

        public void Bind(WsClient client)
        {
            client.MessageReceived += OnMessage;
        }

        public void Unbind(WsClient client)
        {
            client.MessageReceived -= OnMessage;
        }

        private void OnMessage(Envelope envelope)
        {
            var payload = envelope.Payload;
            switch (envelope.Type)
            {
                case "lobby_state":
                    HandleLobbyState(payload.ToObject<LobbyStatePayload>());
                    break;
                case "character_locked":
                    HandleCharacterLocked(payload.ToObject<CharacterLockedPayload>());
                    break;
                case "match_start":
                    HandleMatchStart(payload.ToObject<MatchStartPayload>());
                    break;
                case "question_push":
                    CurrentQuestion = payload.ToObject<QuestionPushPayload>();
                    QuestionNumber = CurrentQuestion.QuestionNumber;
                    QuestionPushed?.Invoke(CurrentQuestion);
                    break;
                case "answer_result":
                    CurrentQuestion = null;
                    AnswerResultReceived?.Invoke(payload.ToObject<AnswerResultPayload>());
                    break;
                case "player_advanced":
                    HandlePlayerAdvanced(payload.ToObject<PlayerAdvancedPayload>());
                    break;
                case "attack_result":
                    HandleAttackResult(payload.ToObject<AttackResultPayload>());
                    break;
                case "freeze_result":
                    HandleFreezeResult(payload.ToObject<FreezeResultPayload>());
                    break;
                case "player_eliminated":
                    HandlePlayerEliminated(payload.ToObject<PlayerEliminatedPayload>());
                    break;
                case "match_end":
                    HandleMatchEnd(payload.ToObject<MatchEndPayload>());
                    break;
                case "xp_award":
                    LastXpAward = payload.ToObject<XpAwardPayload>();
                    XpAwarded?.Invoke(LastXpAward);
                    break;
                case "live_dashboard":
                    HandleDashboard(payload.ToObject<LiveDashboardPayload>());
                    break;
                case "error":
                    ServerError?.Invoke(payload.ToObject<ErrorPayload>());
                    break;
            }
        }

        private void HandleLobbyState(LobbyStatePayload state)
        {
            MatchId = state.MatchId;
            Mode = state.Mode;
            LobbyPlayers = new List<ClientLobbyPlayer>();
            foreach (var p in state.Players)
            {
                LobbyPlayers.Add(new ClientLobbyPlayer { playerId = p.PlayerId, name = p.Name, characterId = p.CharacterId, team = p.Team, ready = p.Ready });
            }
            LobbyUpdated?.Invoke(state);
        }

        private void HandleCharacterLocked(CharacterLockedPayload locked)
        {
            var entry = LobbyPlayers.Find(p => p.playerId == locked.PlayerId);
            if (entry != null) entry.characterId = locked.CharacterId;
            CharacterLocked?.Invoke(locked);
        }

        private void HandleMatchStart(MatchStartPayload start)
        {
            Status = "active";
            GridWidth = start.ArenaLayout.Grid.Width;
            GridHeight = start.ArenaLayout.Grid.Height;
            GoalRow = start.ArenaLayout.GoalRow;

            Players.Clear();
            foreach (var p in start.Players)
            {
                Players[p.PlayerId] = new ClientPlayerState
                {
                    playerId = p.PlayerId,
                    name = p.Name,
                    characterId = p.CharacterId,
                    team = p.Team,
                    hp = p.Hp,
                    maxHp = p.MaxHp,
                    pos = new Vector2Int(p.Pos.X, p.Pos.Y),
                    alive = p.Alive,
                };
            }
            MatchStarted?.Invoke(start);
        }

        private void HandlePlayerAdvanced(PlayerAdvancedPayload advanced)
        {
            if (Players.TryGetValue(advanced.PlayerId, out var p))
            {
                p.pos = new Vector2Int(advanced.NewGridPos.X, advanced.NewGridPos.Y);
                p.hp = advanced.Hp;
                p.maxHp = advanced.MaxHp;
                p.alive = advanced.Alive;
                p.streak = advanced.Streak;
                p.goalReached = advanced.GoalReached;
                p.frozen = advanced.Frozen;
            }
            PlayerAdvanced?.Invoke(advanced);
        }

        private void HandleAttackResult(AttackResultPayload attack)
        {
            if (Players.TryGetValue(attack.TargetId, out var target))
            {
                target.hp = attack.TargetHpAfter;
                target.alive = !attack.Eliminated;
            }
            AttackResolved?.Invoke(attack);
        }

        private void HandleFreezeResult(FreezeResultPayload freeze)
        {
            // Optimistic — the authoritative frozen flag rides along on the target's next
            // player_advanced (once it's actually consumed/cleared server-side).
            if (Players.TryGetValue(freeze.TargetId, out var target)) target.frozen = true;
            FreezeResolved?.Invoke(freeze);
        }

        private void HandlePlayerEliminated(PlayerEliminatedPayload elim)
        {
            if (Players.TryGetValue(elim.PlayerId, out var p)) p.alive = false;
            PlayerEliminated?.Invoke(elim.PlayerId);
        }

        private void HandleMatchEnd(MatchEndPayload end)
        {
            Status = "completed";
            MatchResult = end;
            MatchEnded?.Invoke(end);
        }

        private void HandleDashboard(LiveDashboardPayload dashboard)
        {
            Status = dashboard.Status;
            DashboardUpdated?.Invoke(dashboard);
        }
    }
}
