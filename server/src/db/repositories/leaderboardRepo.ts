import { db } from "../client";

export interface LeaderboardEntry {
  student_profile_id: number;
  name: string;
  xp_total: number;
  class_roster_id: number;
}

export function leaderboardForClass(classRosterId: number, limit = 20): LeaderboardEntry[] {
  return db
    .prepare(
      `SELECT id AS student_profile_id, name, xp_total, class_roster_id
       FROM student_profiles WHERE class_roster_id = ? ORDER BY xp_total DESC LIMIT ?`
    )
    .all(classRosterId, limit) as LeaderboardEntry[];
}

export function leaderboardGlobal(limit = 20): LeaderboardEntry[] {
  return db
    .prepare(
      `SELECT id AS student_profile_id, name, xp_total, class_roster_id
       FROM student_profiles ORDER BY xp_total DESC LIMIT ?`
    )
    .all(limit) as LeaderboardEntry[];
}
