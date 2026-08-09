import { db } from "../client";

export interface StudentProfile {
  id: number;
  class_roster_id: number;
  name: string;
  pin_hash: string;
  xp_total: number;
  created_at: number;
}

export function createStudentProfile(classRosterId: number, name: string, pinHash: string): StudentProfile {
  const stmt = db.prepare(
    "INSERT INTO student_profiles (class_roster_id, name, pin_hash, xp_total, created_at) VALUES (?, ?, ?, 0, ?)"
  );
  const info = stmt.run(classRosterId, name, pinHash, Date.now());
  return findStudentProfileById(info.lastInsertRowid as number)!;
}

export function findStudentProfileById(id: number): StudentProfile | undefined {
  return db.prepare("SELECT * FROM student_profiles WHERE id = ?").get(id) as StudentProfile | undefined;
}

export function findStudentProfileByName(classRosterId: number, name: string): StudentProfile | undefined {
  return db
    .prepare("SELECT * FROM student_profiles WHERE class_roster_id = ? AND name = ?")
    .get(classRosterId, name) as StudentProfile | undefined;
}

export function listStudentProfilesByClass(classRosterId: number): StudentProfile[] {
  return db
    .prepare("SELECT * FROM student_profiles WHERE class_roster_id = ? ORDER BY name")
    .all(classRosterId) as StudentProfile[];
}

export function addXp(studentProfileId: number, amount: number): StudentProfile {
  db.prepare("UPDATE student_profiles SET xp_total = xp_total + ? WHERE id = ?").run(amount, studentProfileId);
  return findStudentProfileById(studentProfileId)!;
}

export function listUnlockedCharacterIds(studentProfileId: number): string[] {
  const rows = db
    .prepare("SELECT character_id FROM character_unlocks WHERE student_profile_id = ?")
    .all(studentProfileId) as { character_id: string }[];
  return rows.map((r) => r.character_id);
}

export function unlockCharacter(studentProfileId: number, characterId: string): void {
  db.prepare(
    "INSERT OR IGNORE INTO character_unlocks (student_profile_id, character_id, unlocked_at) VALUES (?, ?, ?)"
  ).run(studentProfileId, characterId, Date.now());
}
