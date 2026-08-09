import { db } from "../client";

export interface ClassRoster {
  id: number;
  teacher_id: number;
  name: string;
  class_code: string;
  created_at: number;
}

export function createClassRoster(teacherId: number, name: string, classCode: string): ClassRoster {
  const stmt = db.prepare(
    "INSERT INTO class_rosters (teacher_id, name, class_code, created_at) VALUES (?, ?, ?, ?)"
  );
  const info = stmt.run(teacherId, name, classCode, Date.now());
  return findClassRosterById(info.lastInsertRowid as number)!;
}

export function findClassRosterById(id: number): ClassRoster | undefined {
  return db.prepare("SELECT * FROM class_rosters WHERE id = ?").get(id) as ClassRoster | undefined;
}

export function findClassRosterByCode(classCode: string): ClassRoster | undefined {
  return db.prepare("SELECT * FROM class_rosters WHERE class_code = ?").get(classCode) as
    | ClassRoster
    | undefined;
}

export function listClassRostersByTeacher(teacherId: number): ClassRoster[] {
  return db.prepare("SELECT * FROM class_rosters WHERE teacher_id = ? ORDER BY name").all(teacherId) as ClassRoster[];
}

export function deleteClassRoster(id: number): void {
  db.prepare("DELETE FROM class_rosters WHERE id = ?").run(id);
}

export function renameClassRoster(id: number, name: string): void {
  db.prepare("UPDATE class_rosters SET name = ? WHERE id = ?").run(name, id);
}
