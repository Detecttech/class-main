import { apiGet } from "./client";

export interface LeaderboardEntry {
  student_profile_id: number;
  name: string;
  xp_total: number;
  class_roster_id: number;
}

export const fetchLeaderboard = (scope: "global" | `class:${number}`) =>
  apiGet<LeaderboardEntry[]>(`/leaderboard?scope=${scope}`);
