import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import {
  createQuestionBank,
  deleteQuestionBank,
  findQuestionBankById,
  listQuestionBanksByTeacher,
  updateQuestionBankName,
} from "../../db/repositories/questionBankRepo";
import {
  createQuestion,
  createQuestionsBulk,
  deleteQuestion,
  findQuestionById,
  listQuestionsByBank,
  updateQuestion,
  type NewQuestion,
} from "../../db/repositories/questionRepo";

export const questionBankRoutes = Router();

questionBankRoutes.use(requireTeacher);

function ownedBankOrNull(bankId: number, teacherId: number) {
  const bank = findQuestionBankById(bankId);
  return bank && bank.teacher_id === teacherId ? bank : null;
}

questionBankRoutes.get("/question-banks", (req: AuthedRequest, res) => {
  res.json(listQuestionBanksByTeacher(req.teacherId!));
});

questionBankRoutes.post("/question-banks", (req: AuthedRequest, res) => {
  const { name } = req.body ?? {};
  if (!name) {
    res.status(400).json({ code: "bad_request", message: "name required" });
    return;
  }
  res.status(201).json(createQuestionBank(req.teacherId!, name));
});

questionBankRoutes.put("/question-banks/:id", (req: AuthedRequest, res) => {
  const bank = ownedBankOrNull(Number(req.params.id), req.teacherId!);
  if (!bank) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  const { name } = req.body ?? {};
  if (!name) {
    res.status(400).json({ code: "bad_request", message: "name required" });
    return;
  }
  updateQuestionBankName(bank.id, name);
  res.json(findQuestionBankById(bank.id));
});

questionBankRoutes.delete("/question-banks/:id", (req: AuthedRequest, res) => {
  const bank = ownedBankOrNull(Number(req.params.id), req.teacherId!);
  if (!bank) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  deleteQuestionBank(bank.id);
  res.status(204).send();
});

questionBankRoutes.get("/question-banks/:id/questions", (req: AuthedRequest, res) => {
  const bank = ownedBankOrNull(Number(req.params.id), req.teacherId!);
  if (!bank) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  res.json(listQuestionsByBank(bank.id));
});

function parseNewQuestion(body: unknown): NewQuestion | null {
  const b = body as Partial<NewQuestion> & { choices?: unknown };
  if (
    typeof b?.text !== "string" ||
    !Array.isArray(b.choices) ||
    b.choices.length !== 4 ||
    !b.choices.every((c) => typeof c === "string") ||
    typeof b.correctIndex !== "number" ||
    b.correctIndex < 0 ||
    b.correctIndex > 3
  ) {
    return null;
  }
  return { text: b.text, choices: b.choices as [string, string, string, string], correctIndex: b.correctIndex };
}

questionBankRoutes.post("/question-banks/:id/questions", (req: AuthedRequest, res) => {
  const bank = ownedBankOrNull(Number(req.params.id), req.teacherId!);
  if (!bank) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  const q = parseNewQuestion(req.body);
  if (!q) {
    res.status(400).json({ code: "bad_request", message: "text, choices[4], correctIndex(0-3) required" });
    return;
  }
  res.status(201).json(createQuestion(bank.id, q));
});

// Bulk import: { questionBankId, questions: NewQuestion[] } — accepts pre-parsed JSON rows
// (CSV parsing happens client-side in the portal so the server only ever sees validated JSON).
questionBankRoutes.post("/question-banks/import", (req: AuthedRequest, res) => {
  const { questionBankId, questions } = req.body ?? {};
  const bank = ownedBankOrNull(Number(questionBankId), req.teacherId!);
  if (!bank) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  if (!Array.isArray(questions) || questions.length === 0) {
    res.status(400).json({ code: "bad_request", message: "questions[] required" });
    return;
  }
  const parsed: NewQuestion[] = [];
  for (const raw of questions) {
    const q = parseNewQuestion(raw);
    if (!q) {
      res.status(400).json({ code: "bad_request", message: "Every question needs text, choices[4], correctIndex(0-3)" });
      return;
    }
    parsed.push(q);
  }
  res.status(201).json(createQuestionsBulk(bank.id, parsed));
});

export const questionRoutes = Router();
questionRoutes.use(requireTeacher);

function ownedQuestionOrNull(questionId: number, teacherId: number) {
  const question = findQuestionById(questionId);
  if (!question) return null;
  return ownedBankOrNull(question.question_bank_id, teacherId) ? question : null;
}

questionRoutes.put("/questions/:id", (req: AuthedRequest, res) => {
  const question = ownedQuestionOrNull(Number(req.params.id), req.teacherId!);
  if (!question) {
    res.status(404).json({ code: "not_found", message: "Question not found" });
    return;
  }
  const q = parseNewQuestion(req.body);
  if (!q) {
    res.status(400).json({ code: "bad_request", message: "text, choices[4], correctIndex(0-3) required" });
    return;
  }
  updateQuestion(question.id, q);
  res.status(204).send();
});

questionRoutes.delete("/questions/:id", (req: AuthedRequest, res) => {
  const question = ownedQuestionOrNull(Number(req.params.id), req.teacherId!);
  if (!question) {
    res.status(404).json({ code: "not_found", message: "Question not found" });
    return;
  }
  deleteQuestion(question.id);
  res.status(204).send();
});
