using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Caching;
using ClinicalTrials.Mcp.Data;
using ClinicalTrials.Mcp.Health;
using ClinicalTrials.Mcp.RateLimiting;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Mcp.Tools;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using StackExchange.Redis;

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
    .ValidateOnStart();

builder.Services.AddOptions<ClientRegistryDatabaseSettings>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        settings.ConnectionString = configuration.GetConnectionString("ClientRegistry") ?? string.Empty;
    })
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.ConnectionString),
        "Configuration 'ConnectionStrings:ClientRegistry' is required.")
    .ValidateOnStart();

builder.Services.AddOptions<StudyLookupCacheSettings>()
    .BindConfiguration(StudyLookupCacheSettings.SectionName)
    .Validate(settings => settings.SuccessTtlSeconds > 0, $"Configuration '{StudyLookupCacheSettings.SectionName}:SuccessTtlSeconds' must be greater than zero.")
    .Validate(settings => settings.NotFoundTtlSeconds >= 0, $"Configuration '{StudyLookupCacheSettings.SectionName}:NotFoundTtlSeconds' must be zero or greater.")
    .ValidateOnStart();

builder.Services.AddOptions<McpRateLimitingSettings>()
    .BindConfiguration(McpRateLimitingSettings.SectionName)
    .Validate(settings => settings.WindowSeconds > 0, $"Configuration '{McpRateLimitingSettings.SectionName}:WindowSeconds' must be greater than zero.")
    .Validate(settings => settings.PerClientPermitLimit > 0, $"Configuration '{McpRateLimitingSettings.SectionName}:PerClientPermitLimit' must be greater than zero.")
    .Validate(settings => settings.GlobalPermitLimit > 0, $"Configuration '{McpRateLimitingSettings.SectionName}:GlobalPermitLimit' must be greater than zero.")
    .ValidateOnStart();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();

var cacheProvider = builder.Configuration.GetValue<CacheProvider>($"{StudyLookupCacheSettings.SectionName}:Provider");
var rateLimitProvider = builder.Configuration.GetValue<RateLimitProvider>($"{McpRateLimitingSettings.SectionName}:Provider");
if (cacheProvider == CacheProvider.Redis || rateLimitProvider == RateLimitProvider.Redis)
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Configuration 'ConnectionStrings:Redis' is required when a Redis-backed provider is enabled.");

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
    });
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
}

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
        policy.RequireClaim(ApiKeyAuthenticationDefaults.ApiKeyActiveClaimType, bool.TrueString);
    });

builder.Services.AddHealthChecks()
    .AddCheck<McpConfigurationHealthCheck>(
        "mcp_configuration",
        tags: ["ready"]);

builder.Services.AddScoped<IClientRegistryStore, SqlClientRegistryStore>();

if (cacheProvider == CacheProvider.Redis)
{
    builder.Services.AddScoped<IStudyLookupCacheStore, DistributedStudyLookupCacheStore>();
}
else
{
    builder.Services.AddScoped<IStudyLookupCacheStore, MemoryStudyLookupCacheStore>();
}

if (rateLimitProvider == RateLimitProvider.Redis)
{
    builder.Services.AddSingleton<IMcpRateLimitStore, RedisMcpRateLimitStore>();
}
else
{
    builder.Services.AddSingleton<IMcpRateLimitStore, MemoryMcpRateLimitStore>();
}

builder.Services.AddHttpClient(StudyServiceHttpClient.ClientName, (services, client) =>
{
    var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<StudyServiceSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
});
builder.Services.AddScoped<StudyServiceHttpClient>();
builder.Services.AddScoped<IStudyLookupEndpointClient, CachedStudyLookupEndpointClient>();

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
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase),
    branch => branch.UseMiddleware<McpRateLimitingMiddleware>());
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
