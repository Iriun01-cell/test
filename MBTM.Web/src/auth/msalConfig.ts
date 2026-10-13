import {
  LogLevel,
  PublicClientApplication,
  type Configuration,
} from "@azure/msal-browser";

// Microsoft Entra External ID(外部テナント)の設定。値は .env.local で指定する(.env.example 参照)
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID as string | undefined;
const authority = import.meta.env.VITE_ENTRA_AUTHORITY as string | undefined;
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE as string | undefined;

export const isAuthConfigured = Boolean(clientId && authority);

// 既定の ciamlogin.com ドメインでは knownAuthorities は不要(カスタムドメイン使用時のみ設定する)
const authorityHost = authority ? new URL(authority).host : "";
const knownAuthorities =
  authorityHost && !authorityHost.endsWith(".ciamlogin.com") ? [authorityHost] : [];

const msalConfig: Configuration = {
  auth: {
    clientId: clientId ?? "00000000-0000-0000-0000-000000000000",
    authority: authority ?? "https://login.microsoftonline.com/common",
    knownAuthorities,
    redirectUri: window.location.origin,
  },
  cache: { cacheLocation: "sessionStorage" },
  system: {
    // MSALのログは警告以上のみ出力(個人情報を含むログは出さない)
    loggerOptions: {
      logLevel: LogLevel.Warning,
      piiLoggingEnabled: false,
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        if (level === LogLevel.Error) console.error(`[msal] ${message}`);
        else if (level === LogLevel.Warning) console.warn(`[msal] ${message}`);
      },
    },
  },
};

export const msalInstance = new PublicClientApplication(msalConfig);

export const loginRequest = {
  scopes: ["openid", "profile", ...(apiScope ? [apiScope] : [])],
};
