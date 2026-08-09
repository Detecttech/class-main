import { useEffect, useState } from "react";
import { listClasses, type ClassRoster } from "../api/classes";
import { fetchLeaderboard, type LeaderboardEntry } from "../api/leaderboard";

export function LeaderboardPage() {
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [scope, setScope] = useState<"global" | number>("global");
  const [entries, setEntries] = useState<LeaderboardEntry[]>([]);

  useEffect(() => {
    listClasses().then(setClasses).catch(() => {});
  }, []);

  useEffect(() => {
    const scopeParam = scope === "global" ? "global" : (`class:${scope}` as const);
    fetchLeaderboard(scopeParam).then(setEntries).catch(() => setEntries([]));
  }, [scope]);

  return (
    <div className="page">
      <h1>Leaderboard</h1>
      <div className="field" style={{ maxWidth: 260 }}>
        <label htmlFor="scope">Scope</label>
        <select
          id="scope"
          value={scope}
          onChange={(e) => setScope(e.target.value === "global" ? "global" : Number(e.target.value))}
        >
          <option value="global">All classes</option>
          {classes.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>
      </div>
      {entries.length === 0 ? (
        <p className="muted">No XP recorded yet.</p>
      ) : (
        <table>
          <thead><tr><th>#</th><th>Student</th><th>XP</th></tr></thead>
          <tbody>
            {entries.map((e, i) => (
              <tr key={e.student_profile_id}>
                <td>{i + 1}</td>
                <td>{e.name}</td>
                <td>{e.xp_total}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
