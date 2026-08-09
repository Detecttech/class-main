import { Router } from "express";
import { leaderboardForClass, leaderboardGlobal } from "../../db/repositories/leaderboardRepo";

export const leaderboardRoutes = Router();

// Public read: students see this post-match too, not just teachers.
leaderboardRoutes.get("/leaderboard", (req, res) => {
  const scope = String(req.query.scope ?? "global");
  if (scope.startsWith("class:")) {
    const classId = Number(scope.slice("class:".length));
    res.json(leaderboardForClass(classId));
    return;
  }
  res.json(leaderboardGlobal());
});
