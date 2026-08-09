// Dev helper: seeds a teacher/class/question-bank/match on a running server and writes
// the resulting IDs to disk so Assets/Editor/NetworkedMatchDemoRunner.cs (Unity) can pick
// them up without duplicating REST-bootstrap logic in C#. Run this, then run the Unity
// method, any time you want to re-exercise the full networked match flow.
import { writeFileSync } from "node:fs";

const BASE = "http://localhost:7777";

async function api(method, path, body, token) {
  const res = await fetch(BASE + path, {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  const json = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`${method} ${path} -> ${res.status}: ${text}`);
  return json;
}

async function main() {
  const mode = process.argv[2] === "teams" ? "teams" : "ffa";
  const username = `unitytest_${Date.now()}`;
  const password = "pw12345";
  const reg = await api("POST", "/api/auth/teacher/register", {
    username,
    password,
    displayName: "Unity Test Teacher",
  });
  const token = reg.token;

  const cls = await api("POST", "/api/classes", { name: "Unity E2E Class" }, token);
  await api("POST", `/api/classes/${cls.id}/roster`, { name: "Alice" }, token);
  const bank = await api("POST", "/api/question-banks", { name: "Unity E2E Bank" }, token);

  // All questions share correctIndex=1 so a client that always answers "1" gets a
  // reliable, fast-forming streak — keeps the networked demo bounded and deterministic.
  const questions = Array.from({ length: 20 }, (_, i) => ({
    text: `Demo question #${i + 1}`,
    choices: ["Wrong A", "Correct", "Wrong C", "Wrong D"],
    correctIndex: 1,
  }));
  await api("POST", "/api/question-banks/import", { questionBankId: bank.id, questions }, token);

  const match = await api("POST", "/api/matches", { classRosterId: cls.id, questionBankId: bank.id, mode }, token);

  const out = {
    matchId: match.id,
    joinCode: match.join_code,
    teacherToken: token,
    classCode: cls.class_code,
    teacherUsername: username,
    teacherPassword: password,
  };
  writeFileSync("D:/temp/match-bootstrap.json", JSON.stringify(out, null, 2));
  console.log(JSON.stringify(out));
}

main().catch((err) => {
  console.error("BOOTSTRAP FAILED:", err);
  process.exit(1);
});
