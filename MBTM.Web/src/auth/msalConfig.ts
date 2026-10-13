import { PublicClientApplication, type Configuration } from "@azure/msal-browser";

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
};

export const msalInstance = new PublicClientApplication(msalConfig);

export const loginRequest = {
  scopes: ["openid", "profile", ...(apiScope ? [apiScope] : [])],
};
