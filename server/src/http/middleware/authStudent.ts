import type { Request, Response, NextFunction } from "express";
import { verifyToken } from "../../auth/jwt";

export interface StudentAuthedRequest extends Request {
  studentProfileId?: number;
  classRosterId?: number;
}

export function requireStudent(req: StudentAuthedRequest, res: Response, next: NextFunction) {
  const header = req.headers.authorization;
  if (!header?.startsWith("Bearer ")) {
    res.status(401).json({ code: "unauthorized", message: "Missing bearer token" });
    return;
  }
  try {
    const payload = verifyToken(header.slice("Bearer ".length));
    if (payload.role !== "student") {
      res.status(403).json({ code: "forbidden", message: "Student role required" });
      return;
    }
    req.studentProfileId = payload.studentProfileId;
    req.classRosterId = payload.classRosterId;
    next();
  } catch {
    res.status(401).json({ code: "unauthorized", message: "Invalid or expired token" });
  }
}
