import { useEffect, useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import {
  createQuestion,
  deleteQuestion,
  importQuestions,
  listQuestionBanks,
  listQuestions,
  updateQuestion,
  type Question,
  type QuestionBank,
  type QuestionInput,
} from "../api/questionBanks";
import { ApiError } from "../api/client";

const emptyForm: QuestionInput = { text: "", choices: ["", "", "", ""], correctIndex: 0 };

export function QuestionBankEditorPage() {
  const { id } = useParams();
  const bankId = Number(id);
  const [bank, setBank] = useState<QuestionBank | null>(null);
  const [questions, setQuestions] = useState<Question[]>([]);
  const [form, setForm] = useState<QuestionInput>(emptyForm);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [csvText, setCsvText] = useState("");
  const [error, setError] = useState<string | null>(null);

  function refresh() {
    listQuestionBanks().then((banks) => setBank(banks.find((b) => b.id === bankId) ?? null));
    listQuestions(bankId)
      .then(setQuestions)
      .catch((e) => setError(e instanceof ApiError ? e.message : String(e)));
  }

  useEffect(refresh, [bankId]);

  function startEdit(q: Question) {
    setEditingId(q.id);
    setForm({ text: q.text, choices: [q.choice_0, q.choice_1, q.choice_2, q.choice_3], correctIndex: q.correct_index });
  }

  function resetForm() {
    setEditingId(null);
    setForm(emptyForm);
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!form.text.trim() || form.choices.some((c) => !c.trim())) {
      setError("Question text and all four choices are required.");
      return;
    }
    try {
      if (editingId) {
        await updateQuestion(editingId, form);
      } else {
        await createQuestion(bankId, form);
      }
      resetForm();
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  async function onDelete(qid: number) {
    if (!confirm("Delete this question?")) return;
    await deleteQuestion(qid);
    if (editingId === qid) resetForm();
    refresh();
  }

  async function onImportCsv() {
    setError(null);
    const rows = csvText
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);
    const parsed: QuestionInput[] = [];
    for (const row of rows) {
      const parts = row.split(",").map((p) => p.trim());
      if (parts.length !== 6) {
        setError(`Each line needs 6 comma-separated fields: text,choiceA,choiceB,choiceC,choiceD,correctIndex(0-3). Problem line: "${row}"`);
        return;
      }
      const correctIndex = Number(parts[5]);
      if (!Number.isInteger(correctIndex) || correctIndex < 0 || correctIndex > 3) {
        setError(`Correct index must be 0-3. Problem line: "${row}"`);
        return;
      }
      parsed.push({ text: parts[0], choices: [parts[1], parts[2], parts[3], parts[4]], correctIndex });
    }
    if (parsed.length === 0) {
      setError("Paste at least one question line to import.");
      return;
    }
    try {
      await importQuestions(bankId, parsed);
      setCsvText("");
      refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  return (
    <div className="page">
      <p><Link to="/question-banks">&larr; All question banks</Link></p>
      <h1>{bank?.name ?? "Question Bank"}</h1>

      <div className="card">
        <h2>{editingId ? "Edit question" : "Add a question"}</h2>
        <form onSubmit={onSubmit}>
          <div className="field">
            <label htmlFor="qtext">Question</label>
            <input id="qtext" value={form.text} onChange={(e) => setForm({ ...form, text: e.target.value })} />
          </div>
          {form.choices.map((choice, i) => (
            <div className="row field" key={i}>
              <input
                type="radio"
                name="correctIndex"
                checked={form.correctIndex === i}
                onChange={() => setForm({ ...form, correctIndex: i })}
                aria-label={`Choice ${i + 1} is correct`}
              />
              <input
                placeholder={`Choice ${i + 1}`}
                value={choice}
                onChange={(e) => {
                  const choices = [...form.choices] as QuestionInput["choices"];
                  choices[i] = e.target.value;
                  setForm({ ...form, choices });
                }}
                style={{ flex: 1 }}
              />
            </div>
          ))}
          <div className="row">
            <button className="btn btn-primary" type="submit">{editingId ? "Save changes" : "Add question"}</button>
            {editingId && <button className="btn" type="button" onClick={resetForm}>Cancel</button>}
          </div>
        </form>
      </div>

      {error && <p className="error-text">{error}</p>}

      <div className="card">
        <h2>Bulk import</h2>
        <p className="muted">One question per line: text,choiceA,choiceB,choiceC,choiceD,correctIndex(0-3)</p>
        <textarea
          rows={5}
          style={{ width: "100%" }}
          placeholder="2+2?,3,4,5,6,1"
          value={csvText}
          onChange={(e) => setCsvText(e.target.value)}
        />
        <div style={{ marginTop: "0.5em" }}>
          <button className="btn btn-primary" onClick={onImportCsv}>Import</button>
        </div>
      </div>

      <h2>Questions ({questions.length})</h2>
      {questions.length === 0 ? (
        <p className="muted">No questions yet.</p>
      ) : (
        <table>
          <thead>
            <tr>
              <th>Question</th>
              <th>Correct answer</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {questions.map((q) => (
              <tr key={q.id}>
                <td>{q.text}</td>
                <td>{[q.choice_0, q.choice_1, q.choice_2, q.choice_3][q.correct_index]}</td>
                <td className="row">
                  <button className="btn" onClick={() => startEdit(q)}>Edit</button>
                  <button className="btn btn-danger" onClick={() => onDelete(q.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
