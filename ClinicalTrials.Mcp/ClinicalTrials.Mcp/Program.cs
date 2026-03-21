using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Health;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Mcp.Tools;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<StudyServiceSettings>()
    .BindConfiguration(StudyServiceSettings.SectionName)
    .Validate(
        settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
        $"Configuration '{StudyServiceSettings.SectionName}:BaseUrl' must be an absolute URI.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.StudyLookupPathTemplate)
            && settings.StudyLookupPathTemplate.Contains(StudyServiceSettings.NctIdPlaceholder, StringComparison.Ordinal),
        $"Configuration '{StudyServiceSettings.SectionName}:StudyLookupPathTemplate' must contain '{StudyServiceSettings.NctIdPlaceholder}'.")
    .ValidateOnStart();

builder.Services.AddOptions<ApiKeyAuthenticationSettings>()
    .BindConfiguration(ApiKeyAuthenticationSettings.SectionName)
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.HeaderName),
        $"Configuration '{ApiKeyAuthenticationSettings.SectionName}:HeaderName' is required.")
    .Validate(
        settings => settings.Clients.Count > 0,
        $"Configuration '{ApiKeyAuthenticationSettings.SectionName}:Clients' must contain at least one API key client.")
    .Validate(
        settings => settings.Clients.All(client =>
            !string.IsNullOrWhiteSpace(client.ClientId)
            && !string.IsNullOrWhiteSpace(client.ApiKey)),
        $"Each configured API key client in '{ApiKeyAuthenticationSettings.SectionName}:Clients' must include non-empty ClientId and ApiKey values.")
    .ValidateOnStart();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
});

builder.Services.AddAuthentication(ApiKeyAuthenticationDefaults.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationDefaults.SchemeName,
        _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ApiKeyAuthenticationDefaults.ActiveClientPolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(ApiKeyAuthenticationDefaults.SchemeName);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(ApiKeyAuthenticationDefaults.ClientActiveClaimType, bool.TrueString);
    });

builder.Services.AddHealthChecks()
    .AddCheck<McpConfigurationHealthCheck>(
        "mcp_configuration",
        tags: ["ready"]);

builder.Services.AddHttpClient(StudyServiceHttpClient.ClientName, (services, client) =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<StudyServiceSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
});
builder.Services.AddScoped<IStudyLookupEndpointClient, StudyServiceHttpClient>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly(typeof(StudiesMcpTools).Assembly);

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Hosting:ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok("healthy"))
    .AllowAnonymous();
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        })
    .AllowAnonymous();
app.MapMcp("/mcp")
    .RequireAuthorization(ApiKeyAuthenticationDefaults.ActiveClientPolicyName);

app.Run();
