import type { ClientConnection, Envelope } from "./wsServer";
import { send } from "./wsServer";
import { handleHello } from "./handlers/connectionHandler";
import { handleJoinLobby, handlePlayerReady, handleSelectCharacter, handleSelectTeam, handleTeacherStartMatch } from "./handlers/lobbyHandler";
import { handleAnswer } from "./handlers/questionHandler";
import { handleRewardConsumed } from "./handlers/rewardHandler";
import { handleAttack } from "./handlers/attackHandler";
import { handleFreeze } from "./handlers/freezeHandler";
import { handleTeacherJoinMatch } from "./handlers/teacherSpectatorHandler";

type Handler = (conn: ClientConnection, msg: Envelope) => void;

const handlers: Record<string, Handler> = {
  hello: handleHello,
  join_lobby: handleJoinLobby,
  select_character: handleSelectCharacter,
  select_team: handleSelectTeam,
  player_ready: handlePlayerReady,
  teacher_start_match: handleTeacherStartMatch,
  submit_answer: handleAnswer,
  reward_consumed: handleRewardConsumed,
  use_attack: handleAttack,
  use_freeze: handleFreeze,
  teacher_join_match: handleTeacherJoinMatch,
};

export function dispatch(conn: ClientConnection, msg: Envelope) {
  const handler = handlers[msg.type];
  if (!handler) {
    send(conn, {
      type: "error",
      correlationId: msg.correlationId,
      payload: { code: "unknown_type", message: `Unhandled message type: ${msg.type}` },
    });
    return;
  }
  // A WS 'message' event handler runs outside any request/response cycle — an uncaught
  // throw here would crash the whole process, taking down every connected client, not
  // just this one. One bad/malicious message must never do that.
  try {
    handler(conn, msg);
  } catch (err) {
    console.error(`[ws] handler for "${msg.type}" threw:`, err);
    send(conn, {
      type: "error",
      correlationId: msg.correlationId,
      payload: { code: "internal_error", message: "Something went wrong handling that message." },
    });
  }
}
