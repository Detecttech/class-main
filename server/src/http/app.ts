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

  if (fs.existsSync(config.webPortalDist)) {
    app.use(express.static(config.webPortalDist));
    app.get("*", (_req, res) => {
      res.sendFile(path.join(config.webPortalDist, "index.html"));
    });
  }

  return app;
}
