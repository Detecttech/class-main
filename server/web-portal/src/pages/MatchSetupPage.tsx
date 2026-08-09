import { useEffect, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { createMatch, listMatches, type Match, type MatchWithClassName } from "../api/matches";
import { ApiError } from "../api/client";

function formatDate(ms: number): string {
  return new Date(ms).toLocaleString();
}

export function MatchSetupPage() {
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [banks, setBanks] = useState<QuestionBank[]>([]);
  const [classId, setClassId] = useState<number | "">("");
  const [bankId, setBankId] = useState<number | "">("");
  const [mode, setMode] = useState<"ffa" | "teams">("ffa");
  const [created, setCreated] = useState<Match | null>(null);
  const [recentMatches, setRecentMatches] = useState<MatchWithClassName[]>([]);
  const [error, setError] = useState<string | null>(null);

  function refreshMatches() {
    listMatches().then(setRecentMatches).catch(() => {});
  }

  useEffect(() => {
    listClasses().then(setClasses).catch(() => {});
    listQuestionBanks().then(setBanks).catch(() => {});
    refreshMatches();
  }, []);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    if (!classId || !bankId) {
      setError("Pick a class and a question bank first.");
      return;
    }
    try {
      const match = await createMatch(Number(classId), Number(bankId), mode);
      setCreated(match);
      refreshMatches();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : String(err));
    }
  }

  return (
    <div className="page" style={{ maxWidth: 700 }}>
      <h1>Match Setup</h1>

      {classes.length === 0 && (
        <p className="muted">You need at least one <Link to="/rosters">class</Link> before starting a match.</p>
      )}
      {banks.length === 0 && (
        <p className="muted">You need at least one <Link to="/question-banks">question bank</Link> before starting a match.</p>
      )}

      <form onSubmit={onSubmit} className="card">
        <div className="field">
          <label htmlFor="class">Class</label>
          <select id="class" value={classId} onChange={(e) => setClassId(e.target.value ? Number(e.target.value) : "")}>
            <option value="">Select a class…</option>
            {classes.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label htmlFor="bank">Question bank</label>
          <select id="bank" value={bankId} onChange={(e) => setBankId(e.target.value ? Number(e.target.value) : "")}>
            <option value="">Select a question bank…</option>
            {banks.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Mode</label>
          <div className="row">
            <label className="row"><input type="radio" checked={mode === "ffa"} onChange={() => setMode("ffa")} /> Free-for-all</label>
            <label className="row"><input type="radio" checked={mode === "teams"} onChange={() => setMode("teams")} /> Teams</label>
          </div>
          {mode === "teams" && (
            <p className="muted">Students pick Team A or Team B themselves in the app's lobby before the match starts — no roster assignment needed here.</p>
          )}
        </div>
        {error && <p className="error-text">{error}</p>}
        <button className="btn btn-primary" type="submit">Create match</button>
      </form>

      {created && (
        <div className="card">
          <h2>Match created</h2>
          <p>Join code (give this to students):</p>
          <p style={{ fontSize: "2em", fontWeight: 700, letterSpacing: "0.1em" }}>{created.join_code}</p>
          <p className="muted">Students enter this code on the app's Connect screen to join the lobby.</p>
          <p className="muted">
            Match ID (for Live Monitor, if you open it separately): <span className="badge">{created.id}</span>
          </p>
          <Link className="btn btn-primary" to={`/live-match?matchId=${created.id}`}>Go to Live Monitor</Link>
        </div>
      )}

      <div className="card">
        <div className="row" style={{ justifyContent: "space-between" }}>
          <h2>Recent matches</h2>
          <button className="btn" onClick={refreshMatches}>Refresh</button>
        </div>
        {recentMatches.length === 0 ? (
          <p className="muted">No matches yet — create one above.</p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Match ID</th>
                <th>Class</th>
                <th>Mode</th>
                <th>Status</th>
                <th>Join code</th>
                <th>Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {recentMatches.map((m) => (
                <tr key={m.id}>
                  <td>{m.id}</td>
                  <td>{m.class_name}</td>
                  <td>{m.mode}</td>
                  <td>{m.status}</td>
                  <td><span className="badge">{m.join_code}</span></td>
                  <td>{formatDate(m.created_at)}</td>
                  <td><Link className="btn" to={`/live-match?matchId=${m.id}`}>Live Monitor</Link></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
