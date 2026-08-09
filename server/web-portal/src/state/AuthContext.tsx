import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { clearToken, getToken } from "../api/client";
import type { Teacher } from "../api/auth";

const TEACHER_KEY = "quizbattle_teacher_info";

interface AuthContextValue {
  teacher: Teacher | null;
  isAuthenticated: boolean;
  signIn: (teacher: Teacher) => void;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function loadStoredTeacher(): Teacher | null {
  const raw = localStorage.getItem(TEACHER_KEY);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as Teacher;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [teacher, setTeacher] = useState<Teacher | null>(() => (getToken() ? loadStoredTeacher() : null));

  useEffect(() => {
    if (teacher) localStorage.setItem(TEACHER_KEY, JSON.stringify(teacher));
  }, [teacher]);

  const signIn = (t: Teacher) => setTeacher(t);
  const signOut = () => {
    clearToken();
    localStorage.removeItem(TEACHER_KEY);
    setTeacher(null);
  };

  return (
    <AuthContext.Provider value={{ teacher, isAuthenticated: Boolean(getToken() && teacher), signIn, signOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
