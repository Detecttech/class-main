import { useEffect, useState } from "react";
import { HashRouter, Routes, Route, Link, useNavigate } from "react-router-dom";
import { fetchServerInfo, type ServerInfo } from "./api/serverInfo";
import { AuthProvider, useAuth } from "./state/AuthContext";
import { RequireAuth } from "./components/RequireAuth";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { QuestionBankListPage } from "./pages/QuestionBankListPage";
import { QuestionBankEditorPage } from "./pages/QuestionBankEditorPage";
import { RosterListPage } from "./pages/RosterListPage";
import { RosterEditorPage } from "./pages/RosterEditorPage";
import { MatchSetupPage } from "./pages/MatchSetupPage";
import { LiveMatchMonitorPage } from "./pages/LiveMatchMonitorPage";
import { LeaderboardPage } from "./pages/LeaderboardPage";

function ConnectionStatus() {
  const [info, setInfo] = useState<ServerInfo | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchServerInfo().then(setInfo).catch((e) => setError(String(e)));
  }, []);

  if (error) return <p className="error-text">Server unreachable: {error}</p>;
  if (!info) return <p className="muted">Connecting to server…</p>;
  return (
    <p className="muted">
      Connected to <strong>{info.serverName}</strong> ({info.mode} mode, v{info.version})
    </p>
  );
}

function Header() {
  const { isAuthenticated, teacher, signOut } = useAuth();
  const navigate = useNavigate();

  function onLogout() {
    signOut();
    navigate("/login");
  }

  return (
    <header style={{ padding: "1rem 1.5rem", borderBottom: "1px solid var(--border)" }}>
      <nav className="row" style={{ justifyContent: "space-between" }}>
        <div className="row">
          <Link to="/">Dashboard</Link>
          <Link to="/question-banks">Question Banks</Link>
          <Link to="/rosters">Rosters</Link>
          <Link to="/match-setup">Match Setup</Link>
          <Link to="/live-match">Live Match</Link>
          <Link to="/leaderboard">Leaderboard</Link>
        </div>
        <div className="row">
          {isAuthenticated ? (
            <>
              <span className="muted">{teacher?.displayName}</span>
              <button className="btn" onClick={onLogout}>Log out</button>
            </>
          ) : (
            <Link to="/login">Login</Link>
          )}
        </div>
      </nav>
      <ConnectionStatus />
    </header>
  );
}

function App() {
  return (
    <AuthProvider>
      <HashRouter>
        <Header />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<RequireAuth><DashboardPage /></RequireAuth>} />
          <Route path="/question-banks" element={<RequireAuth><QuestionBankListPage /></RequireAuth>} />
          <Route path="/question-banks/:id" element={<RequireAuth><QuestionBankEditorPage /></RequireAuth>} />
          <Route path="/rosters" element={<RequireAuth><RosterListPage /></RequireAuth>} />
          <Route path="/rosters/:id" element={<RequireAuth><RosterEditorPage /></RequireAuth>} />
          <Route path="/match-setup" element={<RequireAuth><MatchSetupPage /></RequireAuth>} />
          <Route path="/live-match" element={<RequireAuth><LiveMatchMonitorPage /></RequireAuth>} />
          <Route path="/leaderboard" element={<RequireAuth><LeaderboardPage /></RequireAuth>} />
        </Routes>
      </HashRouter>
    </AuthProvider>
  );
}

export default App;
