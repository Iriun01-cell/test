using Azure.Monitor.OpenTelemetry.Exporter;
using MBTM.Api.Auth;
using MBTM.Api.Tasks;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// トークン検証とTable Storage(設定が無い場合は最初の利用時にエラーになる)
builder.Services.AddSingleton<TokenValidator>();
builder.Services.AddSingleton<TaskRepository>();

// Application Insights(エラー検出)。接続文字列が設定されている場合のみ有効化
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
