import { db } from "../client";

export interface Question {
  id: number;
  question_bank_id: number;
  text: string;
  choice_0: string;
  choice_1: string;
  choice_2: string;
  choice_3: string;
  correct_index: number;
  created_at: number;
}

export interface NewQuestion {
  text: string;
  choices: [string, string, string, string];
  correctIndex: number;
}

export function createQuestion(questionBankId: number, q: NewQuestion): Question {
  const stmt = db.prepare(
    `INSERT INTO questions (question_bank_id, text, choice_0, choice_1, choice_2, choice_3, correct_index, created_at)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?)`
  );
  const info = stmt.run(
    questionBankId,
    q.text,
    q.choices[0],
    q.choices[1],
    q.choices[2],
    q.choices[3],
    q.correctIndex,
    Date.now()
  );
  return findQuestionById(info.lastInsertRowid as number)!;
}

export function createQuestionsBulk(questionBankId: number, questions: NewQuestion[]): Question[] {
  const insertMany = db.transaction((items: NewQuestion[]) => {
    return items.map((q) => createQuestion(questionBankId, q));
  });
  return insertMany(questions);
}

export function findQuestionById(id: number): Question | undefined {
  return db.prepare("SELECT * FROM questions WHERE id = ?").get(id) as Question | undefined;
}

export function listQuestionsByBank(questionBankId: number): Question[] {
  return db
    .prepare("SELECT * FROM questions WHERE question_bank_id = ? ORDER BY id")
    .all(questionBankId) as Question[];
}

export function updateQuestion(id: number, q: NewQuestion): void {
  db.prepare(
    `UPDATE questions SET text = ?, choice_0 = ?, choice_1 = ?, choice_2 = ?, choice_3 = ?, correct_index = ?
     WHERE id = ?`
  ).run(q.text, q.choices[0], q.choices[1], q.choices[2], q.choices[3], q.correctIndex, id);
}

export function deleteQuestion(id: number): void {
  db.prepare("DELETE FROM questions WHERE id = ?").run(id);
}
