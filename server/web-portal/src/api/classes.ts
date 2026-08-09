import { apiDelete, apiGet, apiPost, apiPut } from "./client";

export interface ClassRoster {
  id: number;
  teacher_id: number;
  name: string;
  class_code: string;
  created_at: number;
}

export const listClasses = () => apiGet<ClassRoster[]>("/classes");
export const getClass = (id: number) => apiGet<ClassRoster>(`/classes/${id}`);
export const createClass = (name: string) => apiPost<ClassRoster>("/classes", { name });
export const renameClass = (id: number, name: string) => apiPut<ClassRoster>(`/classes/${id}`, { name });
export const deleteClass = (id: number) => apiDelete<void>(`/classes/${id}`);
