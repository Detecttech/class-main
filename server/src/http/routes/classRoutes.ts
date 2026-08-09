import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import {
  createClassRoster,
  deleteClassRoster,
  findClassRosterById,
  listClassRostersByTeacher,
  renameClassRoster,
} from "../../db/repositories/classRepo";

export const classRoutes = Router();

function randomClassCode(): string {
  const alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  let code = "";
  for (let i = 0; i < 6; i++) code += alphabet[Math.floor(Math.random() * alphabet.length)];
  return code;
}

classRoutes.use(requireTeacher);

classRoutes.get("/classes", (req: AuthedRequest, res) => {
  res.json(listClassRostersByTeacher(req.teacherId!));
});

classRoutes.post("/classes", (req: AuthedRequest, res) => {
  const { name } = req.body ?? {};
  if (!name) {
    res.status(400).json({ code: "bad_request", message: "name required" });
    return;
  }
  const roster = createClassRoster(req.teacherId!, name, randomClassCode());
  res.status(201).json(roster);
});

classRoutes.get("/classes/:id", (req: AuthedRequest, res) => {
  const roster = findClassRosterById(Number(req.params.id));
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  res.json(roster);
});

classRoutes.put("/classes/:id", (req: AuthedRequest, res) => {
  const roster = findClassRosterById(Number(req.params.id));
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  const { name } = req.body ?? {};
  if (!name) {
    res.status(400).json({ code: "bad_request", message: "name required" });
    return;
  }
  renameClassRoster(roster.id, name);
  res.json(findClassRosterById(roster.id));
});

classRoutes.delete("/classes/:id", (req: AuthedRequest, res) => {
  const roster = findClassRosterById(Number(req.params.id));
  if (!roster || roster.teacher_id !== req.teacherId) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  deleteClassRoster(roster.id);
  res.status(204).send();
});
