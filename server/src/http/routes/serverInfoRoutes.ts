import { Router } from "express";
import { config } from "../../config";

export const serverInfoRoutes = Router();

serverInfoRoutes.get("/server/info", (_req, res) => {
  res.json({
    serverName: config.serverName,
    mode: config.mode,
    httpPort: config.httpPort,
    discoveryPort: config.discoveryPort,
    version: "0.1.0",
  });
});
