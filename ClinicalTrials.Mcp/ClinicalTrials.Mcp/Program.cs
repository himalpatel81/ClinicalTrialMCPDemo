using ClinicalTrials.Mcp.Authentication;
using ClinicalTrials.Mcp.Caching;
using ClinicalTrials.Mcp.Data;
using ClinicalTrials.Mcp.Health;
using ClinicalTrials.Mcp.Observability;
using ClinicalTrials.Mcp.RateLimiting;
using ClinicalTrials.Mcp.Runtime;
using ClinicalTrials.Mcp.Services;
using ClinicalTrials.Mcp.Tools;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
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

builder.Services.AddOptions<StudyServiceResilienceSettings>()
    .BindConfiguration(StudyServiceResilienceSettings.SectionName)
    .Validate(settings => settings.TotalRequestTimeoutSeconds > 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:TotalRequestTimeoutSeconds' must be greater than zero.")
    .Validate(settings => settings.AttemptTimeoutSeconds > 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:AttemptTimeoutSeconds' must be greater than zero.")
    .Validate(settings => settings.MaxRetryAttempts >= 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:MaxRetryAttempts' must be zero or greater.")
    .Validate(settings => settings.CircuitBreakerFailureRatio > 0 && settings.CircuitBreakerFailureRatio <= 1, $"Configuration '{StudyServiceResilienceSettings.SectionName}:CircuitBreakerFailureRatio' must be between 0 and 1.")
    .Validate(settings => settings.CircuitBreakerMinimumThroughput > 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:CircuitBreakerMinimumThroughput' must be greater than zero.")
    .Validate(settings => settings.CircuitBreakerSamplingWindowSeconds > 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:CircuitBreakerSamplingWindowSeconds' must be greater than zero.")
    .Validate(settings => settings.CircuitBreakerBreakDurationSeconds > 0, $"Configuration '{StudyServiceResilienceSettings.SectionName}:CircuitBreakerBreakDurationSeconds' must be greater than zero.")
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

builder.Services.AddOptions<McpRequestProtectionSettings>()
    .BindConfiguration(McpRequestProtectionSettings.SectionName)
    .Validate(settings => settings.RequestTimeoutSeconds > 0, $"Configuration '{McpRequestProtectionSettings.SectionName}:RequestTimeoutSeconds' must be greater than zero.")
    .Validate(settings => settings.MaxRequestBodySizeBytes > 0, $"Configuration '{McpRequestProtectionSettings.SectionName}:MaxRequestBodySizeBytes' must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddOptions<SecretsSettings>()
    .BindConfiguration(SecretsSettings.SectionName)
    .Validate(
        settings => settings.Provider != SecretsProvider.AzureKeyVault
            || Uri.TryCreate(settings.AzureKeyVault.VaultUri, UriKind.Absolute, out _),
        $"Configuration '{SecretsSettings.SectionName}:AzureKeyVault:VaultUri' must be an absolute URI when Azure Key Vault is enabled.")
    .ValidateOnStart();

builder.Services.AddOptions<TelemetrySettings>()
    .BindConfiguration(TelemetrySettings.SectionName)
    .ValidateOnStart();

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId
        | ActivityTrackingOptions.TraceId
        | ActivityTrackingOptions.ParentId;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddRequestTimeouts();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<McpTelemetry>();

var cacheProvider = builder.Configuration.GetValue<CacheProvider>($"{StudyLookupCacheSettings.SectionName}:Provider");
var rateLimitProvider = builder.Configuration.GetValue<RateLimitProvider>($"{McpRateLimitingSettings.SectionName}:Provider");
var telemetrySettings = builder.Configuration.GetSection(TelemetrySettings.SectionName).Get<TelemetrySettings>()
    ?? new TelemetrySettings();

var openTelemetryBuilder = builder.Services.AddOpenTelemetry();
openTelemetryBuilder.WithTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options =>
    {
        options.RecordException = true;
    });
    tracing.AddHttpClientInstrumentation();
    tracing.AddSource(McpTelemetry.ActivitySourceName);

    if (telemetrySettings.Console.Enabled)
    {
        tracing.AddConsoleExporter();
    }
});
openTelemetryBuilder.WithMetrics(metrics =>
{
    metrics.AddAspNetCoreInstrumentation();
    metrics.AddHttpClientInstrumentation();
    metrics.AddMeter(McpTelemetry.MeterName);

    if (telemetrySettings.Console.Enabled)
    {
        metrics.AddConsoleExporter();
    }
});

if (!string.IsNullOrWhiteSpace(telemetrySettings.ApplicationInsights.ConnectionString))
{
    openTelemetryBuilder.UseAzureMonitor(options =>
    {
        options.ConnectionString = telemetrySettings.ApplicationInsights.ConnectionString;
    });
}

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

var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<McpConfigurationHealthCheck>(
        "mcp_configuration",
        tags: ["ready"])
    .AddCheck<ClientRegistryDatabaseHealthCheck>(
        "client_registry_database",
        tags: ["ready"])
    .AddCheck<RedisDependencyHealthCheck>(
        "redis_dependency",
        tags: ["ready"]);

builder.Services.AddScoped<IClientRegistryStore, SqlClientRegistryStore>();
builder.Services.AddScoped<IClientRegistryDatabaseProbe, ClientRegistryDatabaseProbe>();

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

if (cacheProvider == CacheProvider.Redis || rateLimitProvider == RateLimitProvider.Redis)
{
    builder.Services.AddSingleton<IRedisDependencyProbe, RedisDependencyProbe>();
}

var studyServiceResilience = builder.Configuration.GetSection(StudyServiceResilienceSettings.SectionName).Get<StudyServiceResilienceSettings>()
    ?? new StudyServiceResilienceSettings();

builder.Services.AddHttpClient(StudyServiceHttpClient.ClientName, (services, client) =>
    {
        var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<StudyServiceSettings>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl);
        client.Timeout = Timeout.InfiniteTimeSpan;
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(studyServiceResilience.TotalRequestTimeoutSeconds);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(studyServiceResilience.AttemptTimeoutSeconds);
        options.Retry.MaxRetryAttempts = studyServiceResilience.MaxRetryAttempts;
        options.CircuitBreaker.FailureRatio = studyServiceResilience.CircuitBreakerFailureRatio;
        options.CircuitBreaker.MinimumThroughput = studyServiceResilience.CircuitBreakerMinimumThroughput;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(studyServiceResilience.CircuitBreakerSamplingWindowSeconds);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(studyServiceResilience.CircuitBreakerBreakDurationSeconds);
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
var requestProtectionSettings = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<McpRequestProtectionSettings>>().Value;

if (app.Configuration.GetValue<bool>("Hosting:ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<McpCorrelationMiddleware>();
app.UseMiddleware<McpRequestTelemetryMiddleware>();
app.UseMiddleware<McpExceptionHandlingMiddleware>();
app.UseRequestTimeouts();
app.UseAuthentication();
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase),
    branch =>
    {
        branch.UseMiddleware<McpRequestProtectionMiddleware>();
        branch.UseMiddleware<McpRateLimitingMiddleware>();
    });
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok("healthy"))
    .AllowAnonymous();
app.MapHealthChecks(
        "/health/ready",
        new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = McpHealthCheckResponseWriter.WriteAsync
        })
    .AllowAnonymous();
var mcpEndpoint = app.MapMcp("/mcp")
    .RequireAuthorization(ApiKeyAuthenticationDefaults.ActiveClientPolicyName);

mcpEndpoint.WithRequestTimeout(TimeSpan.FromSeconds(requestProtectionSettings.RequestTimeoutSeconds));

app.Run();
