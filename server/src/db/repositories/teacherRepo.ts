import { db } from "../client";

export interface Teacher {
  id: number;
  username: string;
  password_hash: string;
  display_name: string;
  created_at: number;
}

export function createTeacher(username: string, passwordHash: string, displayName: string): Teacher {
  const stmt = db.prepare(
    "INSERT INTO teachers (username, password_hash, display_name, created_at) VALUES (?, ?, ?, ?)"
  );
  const info = stmt.run(username, passwordHash, displayName, Date.now());
  return findTeacherById(info.lastInsertRowid as number)!;
}

export function findTeacherByUsername(username: string): Teacher | undefined {
  return db.prepare("SELECT * FROM teachers WHERE username = ?").get(username) as Teacher | undefined;
}

export function findTeacherById(id: number): Teacher | undefined {
  return db.prepare("SELECT * FROM teachers WHERE id = ?").get(id) as Teacher | undefined;
}
