import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { listClasses, type ClassRoster } from "../api/classes";
import { listQuestionBanks, type QuestionBank } from "../api/questionBanks";
import { useAuth } from "../state/AuthContext";

export function DashboardPage() {
  const { teacher } = useAuth();
  const [classes, setClasses] = useState<ClassRoster[]>([]);
  const [banks, setBanks] = useState<QuestionBank[]>([]);

  useEffect(() => {
    listClasses().then(setClasses).catch(() => {});
    listQuestionBanks().then(setBanks).catch(() => {});
  }, []);

  return (
    <div className="page">
      <h1>Welcome{teacher ? `, ${teacher.displayName}` : ""}</h1>
      <div className="row">
        <div className="card" style={{ flex: 1, minWidth: 200 }}>
          <h2>{classes.length}</h2>
          <p className="muted">Classes</p>
          <Link className="btn" to="/rosters">Manage classes</Link>
        </div>
        <div className="card" style={{ flex: 1, minWidth: 200 }}>
          <h2>{banks.length}</h2>
          <p className="muted">Question banks</p>
          <Link className="btn" to="/question-banks">Manage question banks</Link>
        </div>
      </div>
      <div className="card">
        <h2>Start a match</h2>
        <p className="muted">Pick a class, a question bank, and get a join code students can enter.</p>
        <Link className="btn btn-primary" to="/match-setup">Set up a match</Link>
      </div>
    </div>
  );
}
