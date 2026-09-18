using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// 予約データを保存するTable Storageのクライアントを登録
builder.Services.AddSingleton(sp =>
{
    var connectionString = Environment.GetEnvironmentVariable("TableStorageConnectionString")
        ?? throw new InvalidOperationException("TableStorageConnectionString app setting is not configured.");
    var serviceClient = new TableServiceClient(connectionString);
    var tableClient = serviceClient.GetTableClient("Reservations");
    tableClient.CreateIfNotExists();
    return tableClient;
});

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
