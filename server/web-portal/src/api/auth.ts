import { apiPost, setToken } from "./client";

export interface Teacher {
  id: number;
  username: string;
  displayName: string;
}

interface AuthResponse {
  token: string;
  teacher: Teacher;
}

export async function login(username: string, password: string): Promise<Teacher> {
  const res = await apiPost<AuthResponse>("/auth/teacher/login", { username, password });
  setToken(res.token);
  return res.teacher;
}

export async function register(username: string, password: string, displayName: string): Promise<Teacher> {
  const res = await apiPost<AuthResponse>("/auth/teacher/register", { username, password, displayName });
  setToken(res.token);
  return res.teacher;
}
