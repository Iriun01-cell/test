import { ApplicationInsights } from "@microsoft/applicationinsights-web";
import { ReactPlugin } from "@microsoft/applicationinsights-react-js";

// Application Insights(ブラウザ側のエラー検出)。接続文字列が未設定の場合は無効
const connectionString = import.meta.env.VITE_APPINSIGHTS_CONNECTION_STRING as string | undefined;

export const reactPlugin = new ReactPlugin();

export const appInsights = connectionString
  ? new ApplicationInsights({
      config: {
        connectionString,
        extensions: [reactPlugin],
        enableAutoRouteTracking: true,
      },
    })
  : null;

appInsights?.loadAppInsights();
