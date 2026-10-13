import { useState } from "react";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { isAuthConfigured, loginRequest } from "./auth/msalConfig";
import { appInsights } from "./telemetry/appInsights";

function toMessage(e: unknown): string {
  const code = (e as { errorCode?: string }).errorCode;
  const text = e instanceof Error ? `${e.name}: ${e.message}` : String(e);
  return code ? `[${code}] ${text}` : text;
}

function App() {
  const { instance, accounts } = useMsal();
  const [error, setError] = useState<string | null>(null);

  const handleLogin = async () => {
    setError(null);
    try {
      await instance.loginRedirect(loginRequest);
    } catch (e) {
      console.error(e);
      appInsights?.trackException({ exception: e as Error });
      setError(toMessage(e));
    }
  };

  const handleLogout = async () => {
    setError(null);
    try {
      await instance.logoutRedirect();
    } catch (e) {
      console.error(e);
      appInsights?.trackException({ exception: e as Error });
      setError(toMessage(e));
    }
  };

  if (!isAuthConfigured) {
    return (
      <main>
        <h1>会員制タスク管理アプリ</h1>
        <p>
          Entra External ID の設定が未完了です。.env.example を参考に .env.local を作成してください。
        </p>
      </main>
    );
  }

  return (
    <main>
      <h1>会員制タスク管理アプリ</h1>
      <UnauthenticatedTemplate>
        <p>ご利用にはログインが必要です。</p>
        <button onClick={handleLogin}>ログイン</button>
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <p>{accounts[0]?.name ?? accounts[0]?.username} さん、ようこそ。</p>
        <button onClick={handleLogout}>ログアウト</button>
      </AuthenticatedTemplate>
      {error && (
        <pre style={{ color: "crimson", whiteSpace: "pre-wrap", textAlign: "left" }}>
          エラー: {error}
        </pre>
      )}
    </main>
  );
}

export default App;
