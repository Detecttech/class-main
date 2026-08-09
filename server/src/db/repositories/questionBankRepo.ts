import { db } from "../client";

export interface QuestionBank {
  id: number;
  teacher_id: number;
  name: string;
  created_at: number;
}

export function createQuestionBank(teacherId: number, name: string): QuestionBank {
  const stmt = db.prepare("INSERT INTO question_banks (teacher_id, name, created_at) VALUES (?, ?, ?)");
  const info = stmt.run(teacherId, name, Date.now());
  return findQuestionBankById(info.lastInsertRowid as number)!;
}

export function findQuestionBankById(id: number): QuestionBank | undefined {
  return db.prepare("SELECT * FROM question_banks WHERE id = ?").get(id) as QuestionBank | undefined;
}

export function listQuestionBanksByTeacher(teacherId: number): QuestionBank[] {
  return db.prepare("SELECT * FROM question_banks WHERE teacher_id = ? ORDER BY name").all(teacherId) as QuestionBank[];
}

export function updateQuestionBankName(id: number, name: string): void {
  db.prepare("UPDATE question_banks SET name = ? WHERE id = ?").run(name, id);
}

export function deleteQuestionBank(id: number): void {
  db.prepare("DELETE FROM questions WHERE question_bank_id = ?").run(id);
  db.prepare("DELETE FROM question_banks WHERE id = ?").run(id);
}
