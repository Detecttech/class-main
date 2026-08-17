import express from "express";
import fs from "node:fs";
import path from "node:path";
import { config } from "../config";
import { serverInfoRoutes } from "./routes/serverInfoRoutes";
import { authRoutes } from "./routes/authRoutes";
import { classRoutes } from "./routes/classRoutes";
import { rosterRoutes } from "./routes/rosterRoutes";
import { questionBankRoutes, questionRoutes } from "./routes/questionBankRoutes";
import { matchRoutes } from "./routes/matchRoutes";
import { leaderboardRoutes } from "./routes/leaderboardRoutes";
import { studentRoutes } from "./routes/studentRoutes";

export function createHttpApp() {
  const app = express();
  app.use(express.json());

  app.use(
    "/api",
    serverInfoRoutes,
    authRoutes,
    classRoutes,
    rosterRoutes,
    questionBankRoutes,
    questionRoutes,
    matchRoutes,
    leaderboardRoutes,
    studentRoutes
  );

  // Mounted before the web-portal's catch-all below, at its own /play prefix so the
  // two static builds (teacher dashboard at /, game client at /play) don't collide on
  // the same origin — same-origin means the WebGL build's fetch/WebSocket calls back to
  // this server need no CORS configuration at all.
  if (fs.existsSync(config.webGLBuildDist)) {
    app.use("/play", express.static(config.webGLBuildDist));
    app.get("/play", (_req, res) => {
      res.sendFile(path.join(config.webGLBuildDist, "index.html"));
    });
  }

  if (fs.existsSync(config.webPortalDist)) {
    app.use(express.static(config.webPortalDist));
    app.get("*", (_req, res) => {
      res.sendFile(path.join(config.webPortalDist, "index.html"));
    });
  }

  return app;
}
