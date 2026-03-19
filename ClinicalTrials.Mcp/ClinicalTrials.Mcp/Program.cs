using ClinicalTrials.Mcp.Tools;
using ClinicalTrials.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddClinicalTrialsStudyLookup(builder.Configuration);
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(StudiesMcpTools).Assembly);

var app = builder.Build();

app.UseCors();

app.MapGet("/health", () => Results.Ok("healthy"));
app.MapMcp("/mcp");

app.Run();
