import { Router } from "express";
import { requireStudent, type StudentAuthedRequest } from "../middleware/authStudent";
import { findStudentProfileById, listUnlockedCharacterIds } from "../../db/repositories/studentRepo";

export const studentRoutes = Router();

studentRoutes.get("/students/:id/profile", requireStudent, (req: StudentAuthedRequest, res) => {
  const id = Number(req.params.id);
  if (id !== req.studentProfileId) {
    res.status(403).json({ code: "forbidden", message: "Can only view your own profile" });
    return;
  }
  const student = findStudentProfileById(id);
  if (!student) {
    res.status(404).json({ code: "not_found", message: "Student not found" });
    return;
  }
  res.json({
    id: student.id,
    name: student.name,
    xpTotal: student.xp_total,
    unlockedCharacters: listUnlockedCharacterIds(student.id),
  });
});
