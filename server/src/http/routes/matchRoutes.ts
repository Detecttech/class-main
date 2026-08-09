import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import { findClassRosterById } from "../../db/repositories/classRepo";
import { findQuestionBankById } from "../../db/repositories/questionBankRepo";
import {
  createMatch,
  findMatchById,
  listMatchesByTeacher,
  listMatchEvents,
  listParticipants,
  type MatchMode,
} from "../../db/repositories/matchRepo";

export const matchRoutes = Router();

matchRoutes.use(requireTeacher);

function randomJoinCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i++) code += alphabet[Math.floor(Math.random() * alphabet.length)];
  return code;
}

matchRoutes.get("/matches", (req: AuthedRequest, res) => {
  res.json(listMatchesByTeacher(req.teacherId!));
});

matchRoutes.post("/matches", (req: AuthedRequest, res) => {
  const { classRosterId, questionBankId, mode } = req.body ?? {};
  const roster = findClassRosterById(Number(classRosterId));
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  const bank = findQuestionBankById(Number(questionBankId));
  if (!bank || bank.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Question bank not found" });
    return;
  }
  const matchMode: MatchMode = mode === "teams" ? "teams" : "ffa";
  const match = createMatch(roster.id, bank.id, matchMode, randomJoinCode());
  res.status(201).json(match);
});

matchRoutes.get("/matches/:id", (req: AuthedRequest, res) => {
  const match = findMatchById(Number(req.params.id));
  if (!match) {
    res.status(404).json({ code: "not_found", message: "Match not found" });
    return;
  }
  const roster = findClassRosterById(match.class_roster_id);
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Match not found" });
    return;
  }
  res.json({
    ...match,
    participants: listParticipants(match.id),
    events: listMatchEvents(match.id),
  });
});
