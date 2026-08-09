import { apiDelete, apiGet, apiPost } from "./client";

export interface StudentSummary {
  id: number;
  name: string;
  xpTotal: number;
  unlockedCharacters: string[];
  pinSet: boolean;
}

export const listRoster = (classId: number) => apiGet<StudentSummary[]>(`/classes/${classId}/roster`);
export const addStudent = (classId: number, name: string) =>
  apiPost<{ id: number; name: string; xpTotal: number }>(`/classes/${classId}/roster`, { name });
export const removeStudent = (studentId: number) => apiDelete<void>(`/roster/${studentId}`);
export const resetStudentPin = (studentId: number) => apiPost<void>(`/roster/${studentId}/reset-pin`);
