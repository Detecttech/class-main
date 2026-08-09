import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { createClass, deleteClass, listClasses, type ClassRoster } from "../api/classes";
import { ApiError } from "../api/client";

export function RosterListPage() {
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  function refresh() {
    listClasses()
      .then(setClasses)
      .catch((e) => setError(e instanceof ApiError ? e.message : String(e)))
      .finally(() => setLoading(false));
  }

  useEffect(refresh, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim()) return;
    try {
      await createClass(newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  async function onDelete(id: number) {
    if (!confirm("Delete this class and its roster? This cannot be undone.")) return;
    await deleteClass(id);
    refresh();
  }

  return (
    <div className="page">
      <h1>Classes</h1>
      <form onSubmit={onCreate} className="row card">
        <input placeholder="New class name (e.g. Period 3 Science)" value={newName} onChange={(e) => setNewName(e.target.value)} />
        <button className="btn btn-primary" type="submit">Create class</button>
      </form>
      {error && <p className="error-text">{error}</p>}
      {loading ? (
        <p className="muted">Loading…</p>
      ) : classes.length === 0 ? (
        <p className="muted">No classes yet. Create one above to start building a roster.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Class code</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {classes.map((c) => (
              <tr key={c.id}>
                <td><Link to={`/rosters/${c.id}`}>{c.name}</Link></td>
                <td><span className="badge">{c.class_code}</span></td>
                <td>
                  <button className="btn btn-danger" onClick={() => onDelete(c.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
