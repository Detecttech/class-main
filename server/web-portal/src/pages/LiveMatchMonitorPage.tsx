import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { getToken } from "../api/client";

interface LobbyPlayer {
  playerId: number;
  name: string;
  characterId: string | null;
  team: string | null;
  ready: boolean;
}

interface DashboardPlayer {
  playerId: number;
  name: string;
  hp: number;
  alive: boolean;
  streak: number;
}

interface QuestionInfo {
  questionId: number;
  text: string;
  choices: string[];
  roundNumber: number;
}

function wsUrl(): string {
  const proto = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${proto}//${window.location.host}/ws`;
}

export function LiveMatchMonitorPage() {
  const [params, setParams] = useSearchParams();
  const matchIdParam = params.get("matchId") ?? "";
  const [matchIdInput, setMatchIdInput] = useState(matchIdParam);
  const [inputError, setInputError] = useState<string | null>(null);
  const [connected, setConnected] = useState(false);
  const [lobby, setLobby] = useState<LobbyPlayer[] | null>(null);
  const [status, setStatus] = useState<string>("lobby");
  const [players, setPlayers] = useState<DashboardPlayer[]>([]);
  const [round, setRound] = useState(0);
  const [question, setQuestion] = useState<QuestionInfo | null>(null);
  const [matchEnd, setMatchEnd] = useState<{ winnerId: unknown; reason: string } | null>(null);
  const [log, setLog] = useState<string[]>([]);
  const wsRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    if (!matchIdParam) return;
    const ws = new WebSocket(wsUrl());
    wsRef.current = ws;

    ws.onopen = () => {
      ws.send(JSON.stringify({ type: "hello", payload: { role: "teacher", token: getToken() } }));
    };

    ws.onmessage = (evt) => {
      const msg = JSON.parse(evt.data);
      switch (msg.type) {
        case "hello_ack":
          ws.send(JSON.stringify({ type: "teacher_join_match", payload: { matchId: Number(matchIdParam) } }));
          setConnected(true);
          break;
        case "lobby_state":
          setLobby(msg.payload.players);
          break;
        case "live_dashboard":
          setStatus(msg.payload.status);
          setRound(msg.payload.round);
          setPlayers(msg.payload.players);
          break;
        case "match_start":
          setStatus("active");
          setPlayers(msg.payload.players);
          break;
        case "question_push":
          setQuestion(msg.payload);
          setLog((l) => [`Round ${msg.payload.roundNumber}: ${msg.payload.text}`, ...l].slice(0, 20));
          break;
        case "round_resolved":
          setPlayers((prev) =>
            prev.map((p) => {
              const r = msg.payload.results.find((x: { playerId: number }) => x.playerId === p.playerId);
              return r ? { ...p, hp: r.hp, alive: r.alive, streak: r.streak } : p;
            })
          );
          setLog((l) => [`Round resolved — correct answer #${msg.payload.correctIndex + 1}`, ...l].slice(0, 20));
          break;
        case "attack_result":
          setLog((l) => [`Attack: player ${msg.payload.attackerId} hit player ${msg.payload.targetId} for ${msg.payload.damage}`, ...l].slice(0, 20));
          break;
        case "player_eliminated":
          setLog((l) => [`Player ${msg.payload.playerId} eliminated`, ...l].slice(0, 20));
          break;
        case "match_end":
          setStatus("completed");
          setMatchEnd(msg.payload);
          setLog((l) => [`Match ended — winner: ${JSON.stringify(msg.payload.winnerId)} (${msg.payload.reason})`, ...l].slice(0, 20));
          break;
        case "error":
          setLog((l) => [`Error: ${msg.payload.code} — ${msg.payload.message}`, ...l].slice(0, 20));
          break;
      }
    };

    ws.onclose = () => setConnected(false);

    return () => ws.close();
  }, [matchIdParam]);

  function connectToMatch() {
    const trimmed = matchIdInput.trim();
    if (!trimmed) return;
    if (!/^\d+$/.test(trimmed)) {
      setInputError(
        `"${trimmed}" isn't a valid match ID — that's the numeric ID shown after creating a match (e.g. "12"), not the join code students use.`
      );
      return;
    }
    setInputError(null);
    setParams({ matchId: trimmed });
  }

  function startMatch() {
    wsRef.current?.send(JSON.stringify({ type: "teacher_start_match", payload: {} }));
  }

  if (!matchIdParam) {
    return (
      <div className="page" style={{ maxWidth: 420 }}>
        <h1>Live Match Monitor</h1>
        <p className="muted">Enter the numeric match ID (shown after creating a match in Match Setup).</p>
        <div className="row card">
          <input placeholder="Match ID" value={matchIdInput} onChange={(e) => setMatchIdInput(e.target.value)} />
          <button className="btn btn-primary" onClick={connectToMatch}>Connect</button>
        </div>
        {inputError && <p className="error-text">{inputError}</p>}
      </div>
    );
  }

  return (
    <div className="page">
      <h1>Live Match Monitor — #{matchIdParam}</h1>
      <p className="muted">{connected ? "Connected" : "Connecting…"} · status: {status} · round {round}</p>

      {lobby && status === "lobby" && (
        <div className="card">
          <h2>Lobby</h2>
          <table>
            <thead><tr><th>Name</th><th>Character</th><th>Team</th><th>Ready</th></tr></thead>
            <tbody>
              {lobby.map((p) => (
                <tr key={p.playerId}>
                  <td>{p.name}</td>
                  <td>{p.characterId ?? <span className="muted">not picked</span>}</td>
                  <td>{p.team ?? <span className="muted">—</span>}</td>
                  <td>{p.ready ? "✓" : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <button className="btn btn-primary" style={{ marginTop: "0.75em" }} onClick={startMatch}>
            Start match
          </button>
        </div>
      )}

      {question && status === "active" && (
        <div className="card">
          <h2>Current question — round {question.roundNumber}</h2>
          <p>{question.text}</p>
          <ul>{question.choices.map((c, i) => <li key={i}>{c}</li>)}</ul>
        </div>
      )}

      {players.length > 0 && (
        <div className="card">
          <h2>Players</h2>
          <table>
            <thead><tr><th>Name</th><th>HP</th><th>Alive</th><th>Streak</th></tr></thead>
            <tbody>
              {players.map((p) => (
                <tr key={p.playerId}>
                  <td>{p.name}</td>
                  <td>{p.hp}</td>
                  <td>{p.alive ? "✓" : "eliminated"}</td>
                  <td>{p.streak}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {matchEnd && (
        <div className="card">
          <h2>Match complete</h2>
          <p>Winner: {JSON.stringify(matchEnd.winnerId)} ({matchEnd.reason})</p>
        </div>
      )}

      <div className="card">
        <h2>Event log</h2>
        {log.length === 0 ? <p className="muted">Waiting for events…</p> : (
          <ul>{log.map((line, i) => <li key={i} className="muted">{line}</li>)}</ul>
        )}
      </div>
    </div>
  );
}
