import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { login, register } from "../api/auth";
import { ApiError } from "../api/client";
import { useAuth } from "../state/AuthContext";

export function LoginPage() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const { signIn } = useAuth();
  const navigate = useNavigate();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const teacher =
        mode === "login" ? await login(username, password) : await register(username, password, displayName);
      signIn(teacher);
      navigate("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Is the server running?");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="page" style={{ maxWidth: 420 }}>
      <h1>Teacher {mode === "login" ? "Login" : "Sign Up"}</h1>
      <form onSubmit={onSubmit} className="card">
        <div className="field">
          <label htmlFor="username">Username</label>
          <input id="username" value={username} onChange={(e) => setUsername(e.target.value)} required autoFocus />
        </div>
        {mode === "register" && (
          <div className="field">
            <label htmlFor="displayName">Display name</label>
            <input id="displayName" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
          </div>
        )}
        <div className="field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={6}
          />
        </div>
        {error && <p className="error-text">{error}</p>}
        <button className="btn btn-primary" type="submit" disabled={busy}>
          {busy ? "Please wait…" : mode === "login" ? "Log in" : "Create account"}
        </button>
      </form>
      <p className="muted">
        {mode === "login" ? (
          <>
            New teacher?{" "}
            <a href="#" onClick={(e) => { e.preventDefault(); setMode("register"); setError(null); }}>
              Create an account
            </a>
          </>
        ) : (
          <>
            Already have an account?{" "}
            <a href="#" onClick={(e) => { e.preventDefault(); setMode("login"); setError(null); }}>
              Log in
            </a>
          </>
        )}
      </p>
    </div>
  );
}
