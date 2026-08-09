import type { Request, Response, NextFunction } from "express";
import { verifyToken } from "../../auth/jwt";

export interface AuthedRequest extends Request {
  teacherId?: number;
}

export function requireTeacher(req: AuthedRequest, res: Response, next: NextFunction) {
  const header = req.headers.authorization;
  if (!header?.startsWith("Bearer ")) {
    res.status(401).json({ code: "unauthorized", message: "Missing bearer token" });
    return;
  }
  try {
    const payload = verifyToken(header.slice("Bearer ".length));
    if (payload.role !== "teacher") {
      res.status(403).json({ code: "forbidden", message: "Teacher role required" });
      return;
    }
    req.teacherId = payload.teacherId;
    next();
  } catch {
    res.status(401).json({ code: "unauthorized", message: "Invalid or expired token" });
  }
}
