import { useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { getClass, type ClassRoster } from "../api/classes";
import { addStudent, listRoster, removeStudent, resetStudentPin, type StudentSummary } from "../api/roster";
import { ApiError } from "../api/client";

export function RosterEditorPage() {
  const { id } = useParams();
  const classId = Number(id);
  const [cls, setCls] = useState<ClassRoster | null>(null);
  const [students, setStudents] = useState<StudentSummary[]>([]);
  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);

  function refresh() {
    getClass(classId).then(setCls).catch(() => {});
    listRoster(classId)
      .then(setStudents)
      .catch((e) => setError(e instanceof ApiError ? e.message : String(e)));
  }

  useEffect(refresh, [classId]);

  async function onAdd(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim()) return;
    try {
      await addStudent(classId, newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  async function onRemove(studentId: number) {
    if (!confirm("Remove this student from the class?")) return;
    await removeStudent(studentId);
    refresh();
  }

  async function onResetPin(studentId: number) {
    await resetStudentPin(studentId);
    refresh();
  }

  return (
    <div className="page">
      <p><Link to="/rosters">&larr; All classes</Link></p>
      <h1>{cls?.name ?? "Class"}</h1>
      {cls && (
        <p className="muted">
          Students join with class code <span className="badge">{cls.class_code}</span>, their name from this list, and a PIN they set on first login.
        </p>
      )}

      <form onSubmit={onAdd} className="row card">
        <input placeholder="Student name" value={newName} onChange={(e) => setNewName(e.target.value)} />
        <button className="btn btn-primary" type="submit">Add student</button>
      </form>
      {error && <p className="error-text">{error}</p>}

      {students.length === 0 ? (
        <p className="muted">No students yet.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>XP</th>
              <th>Unlocked characters</th>
              <th>PIN</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {students.map((s) => (
              <tr key={s.id}>
                <td>{s.name}</td>
                <td>{s.xpTotal}</td>
                <td>{s.unlockedCharacters.length > 0 ? s.unlockedCharacters.join(", ") : <span className="muted">default only</span>}</td>
                <td>{s.pinSet ? "set" : <span className="muted">not set yet</span>}</td>
                <td className="row">
                  <button className="btn" onClick={() => onResetPin(s.id)} disabled={!s.pinSet}>Reset PIN</button>
                  <button className="btn btn-danger" onClick={() => onRemove(s.id)}>Remove</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
