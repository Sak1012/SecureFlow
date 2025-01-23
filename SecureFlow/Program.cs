using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.AspNetCore.Http.Json;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Load configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("local.settings.json", optional: true)
    .AddEnvironmentVariables();

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON serialization
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Frontend URL
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

// Configuration sections
var azureAd = builder.Configuration.GetSection("AzureAd");
var logAnalytics = builder.Configuration.GetRequiredSection("LogAnalytics");

// Azure authentication
var credential = new ClientSecretCredential(
    azureAd["TenantId"],
    azureAd["ClientId"],
    azureAd["ClientSecret"]
);

// API endpoints
app.MapGet("/activity-logs", async () =>
{
    try
    {
        var logsClient = new LogsQueryClient(credential);

        // Query Log Analytics workspace for Azure Activity logs
        string workspaceId = logAnalytics["WorkspaceId"];
        string query = "AzureActivity";

        var response = await logsClient.QueryWorkspaceAsync(
            workspaceId,
            query,
            TimeSpan.FromDays(1)
        );
        return response.Value.Table.Rows.Count == 0 
            ? Results.NotFound("No logs found") 
            : Results.Ok(response.Value.Table.Rows.Select(row => new ActivityLogDto(
                TimeGenerated: row.GetDateTimeOffset("TimeGenerated"),          
                OperationName: row.GetString("OperationName"),                  
                Category: row.GetString("Category"),
                ActivityStatus: row.GetString("ActivityStatus"),               
                Caller: row.GetString("Caller"),
                ClientIpAddress: row.GetString("CallerIpAddress"),              
                Properties: JsonSerializer.Deserialize<Dictionary<string, object>>(
                    row.GetString("Properties") ?? "{}")
            )));    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Log retrieval failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
})
.WithName("GetActivityLogs")
.Produces<List<ActivityLogDto>>()
.ProducesProblem(500);

app.Run();

// DTO Records
public record ActivityLogDto(
    DateTimeOffset? TimeGenerated,  
    string OperationName,           
    string Category,
    string ActivityStatus,          
    string Caller,
    string ClientIpAddress,         
    Dictionary<string, object> Properties
);