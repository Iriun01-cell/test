import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { apiTokenRequest, msalInstance } from "../auth/msalConfig";

const baseUrl = (
  (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? "http://localhost:7071/api"
).replace(/\/$/, "");

export type TaskStatus = "todo" | "doing" | "done";

export type Task = {
  id: string;
  title: string;
  status: TaskStatus;
  dueDate: string | null;
  memo: string;
  createdAt: string;
  updatedAt: string;
};

export type TaskInput = {
  title: string;
  status?: TaskStatus;
  dueDate?: string | null;
  memo?: string;
};

export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

async function getAccessToken(): Promise<string> {
  if (!apiTokenRequest) {
    throw new Error("VITE_ENTRA_API_SCOPE が未設定です。.env.local を確認してください。");
  }
  const account = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (!account) {
    throw new Error("ログインしていません。");
  }
  try {
    const result = await msalInstance.acquireTokenSilent({ ...apiTokenRequest, account });
    return result.accessToken;
  } catch (e) {
    if (e instanceof InteractionRequiredAuthError) {
      // 同意や再認証が必要な場合は、リダイレクトで取得し直す
      await msalInstance.acquireTokenRedirect({ ...apiTokenRequest, account });
    }
    throw e;
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = await getAccessToken();
  const res = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      ...init.headers,
      Authorization: `Bearer ${token}`,
      ...(init.body ? { "Content-Type": "application/json" } : {}),
    },
  });

  if (!res.ok) {
    let message = `API エラー (${res.status})`;
    try {
      const body = (await res.json()) as { error?: string };
      if (body.error) message = body.error;
    } catch {
      // 本文が JSON でない場合は既定のメッセージを使う
    }
    throw new ApiError(res.status, message);
  }

  return res.status === 204 ? (undefined as T) : ((await res.json()) as T);
}

export const tasksApi = {
  list: () => request<Task[]>("/tasks"),
  create: (input: TaskInput) =>
    request<Task>("/tasks", { method: "POST", body: JSON.stringify(input) }),
  update: (id: string, input: TaskInput) =>
    request<Task>(`/tasks/${id}`, { method: "PUT", body: JSON.stringify(input) }),
  remove: (id: string) => request<void>(`/tasks/${id}`, { method: "DELETE" }),
};
