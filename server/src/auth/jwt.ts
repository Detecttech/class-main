import jwt from "jsonwebtoken";
import { config } from "../config";

export interface TeacherTokenPayload {
  role: "teacher";
  teacherId: number;
}

export interface StudentTokenPayload {
  role: "student";
  studentProfileId: number;
  classRosterId: number;
}

export type TokenPayload = TeacherTokenPayload | StudentTokenPayload;

export function signToken(payload: TokenPayload, expiresIn: string = "12h"): string {
  return jwt.sign(payload, config.jwtSecret, { expiresIn } as jwt.SignOptions);
}

export function verifyToken(token: string): TokenPayload {
  return jwt.verify(token, config.jwtSecret) as TokenPayload;
}
