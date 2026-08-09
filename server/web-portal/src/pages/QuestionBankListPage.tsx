import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { createQuestionBank, deleteQuestionBank, listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { ApiError } from "../api/client";

export function QuestionBankListPage() {
  const [banks, setBanks] = useState<QuestionBank[]>([]);
  const [newName, setNewName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  function refresh() {
    listQuestionBanks()
      .then(setBanks)
      .catch((e) => setError(e instanceof ApiError ? e.message : String(e)))
      .finally(() => setLoading(false));
  }

  useEffect(refresh, []);

  async function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!newName.trim()) return;
    try {
      await createQuestionBank(newName.trim());
      setNewName("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  async function onDelete(id: number) {
    if (!confirm("Delete this question bank and all its questions?")) return;
    await deleteQuestionBank(id);
    refresh();
  }

  return (
    <div className="page">
      <h1>Question Banks</h1>
      <form onSubmit={onCreate} className="row card">
        <input placeholder="New bank name (e.g. Fractions Unit 2)" value={newName} onChange={(e) => setNewName(e.target.value)} />
        <button className="btn btn-primary" type="submit">Create bank</button>
      </form>
      {error && <p className="error-text">{error}</p>}
      {loading ? (
        <p className="muted">Loading…</p>
      ) : banks.length === 0 ? (
        <p className="muted">No question banks yet. Create one above.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {banks.map((b) => (
              <tr key={b.id}>
                <td><Link to={`/question-banks/${b.id}`}>{b.name}</Link></td>
                <td><button className="btn btn-danger" onClick={() => onDelete(b.id)}>Delete</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
