import { apiGet, apiPost } from "./client";

export interface Match {
  id: number;
  class_roster_id: number;
  question_bank_id: number;
  mode: "ffa" | "teams";
  status: "lobby" | "active" | "completed";
  join_code: string;
  winner_ref: string | null;
  started_at: number | null;
  ended_at: number | null;
  created_at: number;
}

export interface MatchWithClassName extends Match {
  class_name: string;
}

export const createMatch = (classRosterId: number, questionBankId: number, mode: "ffa" | "teams") =>
  apiPost<Match>("/matches", { classRosterId, questionBankId, mode });

export const getMatch = (id: number) => apiGet<Match & { participants: unknown[]; events: unknown[] }>(`/matches/${id}`);

export const listMatches = () => apiGet<MatchWithClassName[]>("/matches");
