import { db } from "../client";

export type MatchMode = "ffa" | "teams";
export type MatchStatus = "lobby" | "active" | "completed";

export interface Match {
  id: number;
  class_roster_id: number;
  question_bank_id: number;
  mode: MatchMode;
  status: MatchStatus;
  join_code: string;
  winner_ref: string | null;
  started_at: number | null;
  ended_at: number | null;
  created_at: number;
}

export interface MatchParticipant {
  match_id: number;
  student_profile_id: number;
  character_id: string;
  team: string | null;
  final_hp: number | null;
  final_placement: number | null;
  xp_awarded: number | null;
}

export function createMatch(
  classRosterId: number,
  questionBankId: number,
  mode: MatchMode,
  joinCode: string
): Match {
  const stmt = db.prepare(
    `INSERT INTO matches (class_roster_id, question_bank_id, mode, status, join_code, created_at)
     VALUES (?, ?, ?, 'lobby', ?, ?)`
  );
  const info = stmt.run(classRosterId, questionBankId, mode, joinCode, Date.now());
  return findMatchById(info.lastInsertRowid as number)!;
}

export function findMatchById(id: number): Match | undefined {
  return db.prepare("SELECT * FROM matches WHERE id = ?").get(id) as Match | undefined;
}

export interface MatchWithClassName extends Match {
  class_name: string;
}

export function listMatchesByTeacher(teacherId: number, limit = 50): MatchWithClassName[] {
  return db
    .prepare(
      `SELECT matches.*, class_rosters.name AS class_name
       FROM matches
       JOIN class_rosters ON matches.class_roster_id = class_rosters.id
       WHERE class_rosters.teacher_id = ?
       ORDER BY matches.created_at DESC
       LIMIT ?`
    )
    .all(teacherId, limit) as MatchWithClassName[];
}

export function findMatchByJoinCode(joinCode: string): Match | undefined {
  return db.prepare("SELECT * FROM matches WHERE join_code = ?").get(joinCode) as Match | undefined;
}

export function setMatchStatus(id: number, status: MatchStatus): void {
  if (status === "active") {
    db.prepare("UPDATE matches SET status = ?, started_at = ? WHERE id = ?").run(status, Date.now(), id);
  } else if (status === "completed") {
    db.prepare("UPDATE matches SET status = ?, ended_at = ? WHERE id = ?").run(status, Date.now(), id);
  } else {
    db.prepare("UPDATE matches SET status = ? WHERE id = ?").run(status, id);
  }
}

export function setMatchWinner(id: number, winnerRef: string): void {
  db.prepare("UPDATE matches SET winner_ref = ? WHERE id = ?").run(winnerRef, id);
}

export function upsertParticipant(p: MatchParticipant): void {
  db.prepare(
    `INSERT INTO match_participants (match_id, student_profile_id, character_id, team, final_hp, final_placement, xp_awarded)
     VALUES (@match_id, @student_profile_id, @character_id, @team, @final_hp, @final_placement, @xp_awarded)
     ON CONFLICT(match_id, student_profile_id) DO UPDATE SET
       character_id = excluded.character_id,
       team = excluded.team,
       final_hp = excluded.final_hp,
       final_placement = excluded.final_placement,
       xp_awarded = excluded.xp_awarded`
  ).run(p);
}

export function listParticipants(matchId: number): MatchParticipant[] {
  return db.prepare("SELECT * FROM match_participants WHERE match_id = ?").all(matchId) as MatchParticipant[];
}

export function appendMatchEvent(matchId: number, seq: number, type: string, payload: unknown): void {
  db.prepare(
    "INSERT INTO match_events (match_id, seq, type, payload_json, created_at) VALUES (?, ?, ?, ?, ?)"
  ).run(matchId, seq, type, JSON.stringify(payload), Date.now());
}

export function listMatchEvents(matchId: number): { seq: number; type: string; payload: unknown }[] {
  const rows = db
    .prepare("SELECT seq, type, payload_json FROM match_events WHERE match_id = ? ORDER BY seq")
    .all(matchId) as { seq: number; type: string; payload_json: string }[];
  return rows.map((r) => ({ seq: r.seq, type: r.type, payload: JSON.parse(r.payload_json) }));
}
