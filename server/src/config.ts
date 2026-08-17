import path from "node:path";

export const config = {
  httpPort: Number(process.env.PORT ?? 7777),
  discoveryPort: Number(process.env.DISCOVERY_PORT ?? 7778),
  mode: (process.env.MODE ?? "lan") as "lan" | "wan",
  jwtSecret: process.env.JWT_SECRET ?? "dev-secret-change-me",
  dbPath: process.env.DB_PATH ?? path.join(__dirname, "..", "data", "quizbattle.db"),
  serverName: process.env.SERVER_NAME ?? "Classroom QuizBattle",
  webPortalDist: path.join(__dirname, "..", "web-portal", "dist"),
  // Served at /play (not /) so it doesn't collide with the teacher web-portal, which
  // owns the root of this same origin — see http/app.ts.
  webGLBuildDist: path.join(__dirname, "..", "..", "game-client", "webgl-build"),
};
