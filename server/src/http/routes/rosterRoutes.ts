import { Router } from "express";
import { requireTeacher, type AuthedRequest } from "../middleware/authTeacher";
import { findClassRosterById } from "../../db/repositories/classRepo";
import {
  createStudentProfile,
  findStudentProfileById,
  listStudentProfilesByClass,
  listUnlockedCharacterIds,
} from "../../db/repositories/studentRepo";
import { db } from "../../db/client";

export const rosterRoutes = Router();

rosterRoutes.use(requireTeacher);

function ownedClassOrNull(classId: number, teacherId: number) {
  const roster = findClassRosterById(classId);
  return roster && roster.teacher_id === teacherId ? roster : null;
}

rosterRoutes.get("/classes/:id/roster", (req: AuthedRequest, res) => {
  const roster = ownedClassOrNull(Number(req.params.id), req.teacherId!);
  if (!roster) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  const students = listStudentProfilesByClass(roster.id).map((s) => ({
    id: s.id,
    name: s.name,
    xpTotal: s.xp_total,
    unlockedCharacters: listUnlockedCharacterIds(s.id),
    pinSet: Boolean(s.pin_hash),
  }));
  res.json(students);
});

rosterRoutes.post("/classes/:id/roster", (req: AuthedRequest, res) => {
  const roster = ownedClassOrNull(Number(req.params.id), req.teacherId!);
  if (!roster) {
    res.status(404).json({ code: "not_found", message: "Class not found" });
    return;
  }
  const { name } = req.body ?? {};
  if (!name) {
    res.status(400).json({ code: "bad_request", message: "name required" });
    return;
  }
  // pin_hash starts empty; the student sets it on first login (see authRoutes).
  const student = createStudentProfile(roster.id, name, "");
  res.status(201).json({ id: student.id, name: student.name, xpTotal: student.xp_total });
});

rosterRoutes.delete("/roster/:studentId", (req: AuthedRequest, res) => {
  const student = findStudentProfileById(Number(req.params.studentId));
  if (!student) {
    res.status(404).json({ code: "not_found", message: "Student not found" });
    return;
  }
  const roster = ownedClassOrNull(student.class_roster_id, req.teacherId!);
  if (!roster) {
    res.status(404).json({ code: "not_found", message: "Student not found" });
    return;
  }
  db.prepare("DELETE FROM character_unlocks WHERE student_profile_id = ?").run(student.id);
  db.prepare("DELETE FROM student_profiles WHERE id = ?").run(student.id);
  res.status(204).send();
});

rosterRoutes.post("/roster/:studentId/reset-pin", (req: AuthedRequest, res) => {
  const student = findStudentProfileById(Number(req.params.studentId));
  if (!student) {
    res.status(404).json({ code: "not_found", message: "Student not found" });
    return;
  }
  const roster = ownedClassOrNull(student.class_roster_id, req.teacherId!);
  if (!roster) {
    res.status(404).json({ code: "not_found", message: "Student not found" });
    return;
  }
  db.prepare("UPDATE student_profiles SET pin_hash = '' WHERE id = ?").run(student.id);
  res.status(204).send();
});
