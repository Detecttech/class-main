import { Router } from "express";
import { signToken } from "../../auth/jwt";
import { hashSecret, verifySecret } from "../../auth/passwordHash";
import { createTeacher, findTeacherByUsername } from "../../db/repositories/teacherRepo";
import { findClassRosterByCode } from "../../db/repositories/classRepo";
import { createStudentProfile, findStudentProfileByName } from "../../db/repositories/studentRepo";
import { db } from "../../db/client";

export const authRoutes = Router();

authRoutes.post("/auth/teacher/register", (req, res) => {
  const { username, password, displayName } = req.body ?? {};
  if (!username || !password || !displayName) {
    res.status(400).json({ code: "bad_request", message: "username, password, displayName required" });
    return;
  }
  if (findTeacherByUsername(username)) {
    res.status(409).json({ code: "conflict", message: "username already taken" });
    return;
  }
  const teacher = createTeacher(username, hashSecret(password), displayName);
  const token = signToken({ role: "teacher", teacherId: teacher.id });
  res.status(201).json({ token, teacher: { id: teacher.id, username: teacher.username, displayName: teacher.display_name } });
});

authRoutes.post("/auth/teacher/login", (req, res) => {
  const { username, password } = req.body ?? {};
  const teacher = username ? findTeacherByUsername(username) : undefined;
  if (!teacher || !verifySecret(password ?? "", teacher.password_hash)) {
    res.status(401).json({ code: "invalid_credentials", message: "Invalid username or password" });
    return;
  }
  const token = signToken({ role: "teacher", teacherId: teacher.id });
  res.json({ token, teacher: { id: teacher.id, username: teacher.username, displayName: teacher.display_name } });
});

// Student "name entry" screen: classCode identifies the roster; a student picks their
// own name and it's created on first use (no teacher pre-registration required). First
// login for a given name sets the PIN; subsequent logins with that name verify it — so a
// name still can't be impersonated by someone else without the PIN once claimed.
authRoutes.post("/auth/student/login", (req, res) => {
  const { classCode, name, pin } = req.body ?? {};
  if (!classCode || !name || !pin) {
    res.status(400).json({ code: "bad_request", message: "classCode, name, pin required" });
    return;
  }
  const roster = findClassRosterByCode(classCode);
  if (!roster) {
    res.status(404).json({ code: "not_found", message: "Unknown class code" });
    return;
  }
  let student = findStudentProfileByName(roster.id, name);
  if (!student) {
    student = createStudentProfile(roster.id, name, hashSecret(pin));
  } else if (!student.pin_hash) {
    db.prepare("UPDATE student_profiles SET pin_hash = ? WHERE id = ?").run(hashSecret(pin), student.id);
  } else if (!verifySecret(pin, student.pin_hash)) {
    res.status(401).json({ code: "invalid_credentials", message: "Incorrect PIN" });
    return;
  }
  const token = signToken({ role: "student", studentProfileId: student.id, classRosterId: roster.id }, "6h");
  res.json({ token, student: { id: student.id, name: student.name, xpTotal: student.xp_total } });
});
