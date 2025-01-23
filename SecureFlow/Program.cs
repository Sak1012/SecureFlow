using Azure.Identity;
using Azure.Monitor.Query;
using Microsoft.OpenApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Load configuration from appsettings and local.settings.json
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger and Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Retrieve configuration settings
var azureAd = builder.Configuration.GetSection("AzureAd");
var logAnalytics = builder.Configuration.GetSection("LogAnalytics");

// Create the Azure Credential
var credential = new ClientSecretCredential(
    azureAd["TenantId"],
    azureAd["ClientId"],
    azureAd["ClientSecret"]
);

// Define the API endpoint
app.MapGet("/activity-logs", async () =>
    {
        try
        {
            var logsClient = new LogsQueryClient(credential);

            // Query Log Analytics workspace for Azure Activity logs
            string workspaceId = logAnalytics["WorkspaceId"];
            string query = "AzureActivity | take 10";

            var response = await logsClient.QueryWorkspaceAsync(
                workspaceId,
                query,
                TimeSpan.FromDays(1)
            );

            // Serialize and return results
            return Results.Ok(JsonSerializer.Serialize(response.Value.Table.Rows));
        }
        catch (Exception ex)
        {
            // Log and return an error
            return Results.Problem($"Error fetching activity logs: {ex.Message}");
        }
    })
    .WithName("ActivityLogs")
    .WithOpenApi();


app.Run();