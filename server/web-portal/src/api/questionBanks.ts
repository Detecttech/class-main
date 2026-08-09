import { apiDelete, apiGet, apiPost, apiPut } from "./client";

export interface QuestionBank {
  id: number;
  teacher_id: number;
  name: string;
  created_at: number;
}

export interface Question {
  id: number;
  question_bank_id: number;
  text: string;
  choice_0: string;
  choice_1: string;
  choice_2: string;
  choice_3: string;
  correct_index: number;
  created_at: number;
}

export interface QuestionInput {
  text: string;
  choices: [string, string, string, string];
  correctIndex: number;
}

export const listQuestionBanks = () => apiGet<QuestionBank[]>("/question-banks");
export const createQuestionBank = (name: string) => apiPost<QuestionBank>("/question-banks", { name });
export const renameQuestionBank = (id: number, name: string) => apiPut<QuestionBank>(`/question-banks/${id}`, { name });
export const deleteQuestionBank = (id: number) => apiDelete<void>(`/question-banks/${id}`);

export const listQuestions = (bankId: number) => apiGet<Question[]>(`/question-banks/${bankId}/questions`);
export const createQuestion = (bankId: number, q: QuestionInput) =>
  apiPost<Question>(`/question-banks/${bankId}/questions`, q);
export const updateQuestion = (id: number, q: QuestionInput) => apiPut<void>(`/questions/${id}`, q);
export const deleteQuestion = (id: number) => apiDelete<void>(`/questions/${id}`);
export const importQuestions = (bankId: number, questions: QuestionInput[]) =>
  apiPost<Question[]>("/question-banks/import", { questionBankId: bankId, questions });
