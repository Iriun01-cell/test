import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { isAuthConfigured, loginRequest } from "./auth/msalConfig";

function App() {
  const { instance, accounts } = useMsal();

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
        <button onClick={() => instance.loginRedirect(loginRequest)}>ログイン</button>
      </UnauthenticatedTemplate>
      <AuthenticatedTemplate>
        <p>{accounts[0]?.name ?? accounts[0]?.username} さん、ようこそ。</p>
        <button onClick={() => instance.logoutRedirect()}>ログアウト</button>
      </AuthenticatedTemplate>
    </main>
  );
}

export default App;
