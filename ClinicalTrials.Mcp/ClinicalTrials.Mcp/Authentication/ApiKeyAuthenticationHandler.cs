using System.Security.Claims;
using System.Text.Encodings.Web;
using ClinicalTrials.Mcp.Data;
using ClinicalTrials.Mcp.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Authentication;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyAuthenticationSettings> settings,
    IClientRegistryStore clientRegistryStore,
    McpTelemetry telemetry,
    TimeProvider timeProvider)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly ApiKeyAuthenticationSettings settings = settings.Value;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(this.settings.HeaderName, out var headerValues))
        {
            RecordAuthFailure("missing_header");
            return AuthenticateResult.NoResult();
        }

        if (headerValues.Count != 1)
        {
            RecordAuthFailure("multiple_header_values");
            Logger.LogWarning("Rejected authentication request because multiple API key header values were supplied.");
            return AuthenticateResult.Fail("A single API key header value is required.");
        }

        var providedApiKey = headerValues[0]?.Trim();
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            RecordAuthFailure("empty_header");
            Logger.LogWarning("Rejected authentication request because the API key header value was empty.");
            return AuthenticateResult.Fail("The API key header value cannot be empty.");
        }

        var keyPrefix = ApiKeyProtector.GetLookupPrefix(providedApiKey);
        var candidates = await clientRegistryStore.GetAuthenticationCandidatesAsync(keyPrefix, Context.RequestAborted);
        var matchingCandidate = candidates.FirstOrDefault(candidate =>
            ApiKeyProtector.Verify(
                providedApiKey,
                candidate.SaltBase64,
                candidate.HashBase64,
                candidate.HashIterations,
                candidate.HashAlgorithm));

        if (matchingCandidate is null)
        {
            RecordAuthFailure("invalid_key");
            Logger.LogWarning("Rejected authentication request because the supplied API key was invalid.");
            return AuthenticateResult.Fail("The supplied API key is invalid.");
        }

        var isKeyActive = matchingCandidate.IsCurrentlyActive(timeProvider.GetUtcNow());
        if (!matchingCandidate.ClientIsActive || !isKeyActive)
        {
            RecordAuthFailure("inactive_client_or_key");
            Logger.LogWarning(
                "Authenticated request for client {ClientCode} will be rejected by authorization because the client or key is inactive.",
                matchingCandidate.ClientCode);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, matchingCandidate.ClientCode),
            new Claim(ClaimTypes.Name, matchingCandidate.ClientCode),
            new Claim(ApiKeyAuthenticationDefaults.ClientIdClaimType, matchingCandidate.ClientCode),
            new Claim(ApiKeyAuthenticationDefaults.ClientActiveClaimType, matchingCandidate.ClientIsActive.ToString())
        }.ToList();

        claims.Add(new Claim(ApiKeyAuthenticationDefaults.ApiKeyActiveClaimType, isKeyActive.ToString()));

        if (matchingCandidate.PermitLimitOverride.HasValue)
        {
            claims.Add(new Claim(
                ApiKeyAuthenticationDefaults.PermitLimitClaimType,
                matchingCandidate.PermitLimitOverride.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    private void RecordAuthFailure(string reason)
    {
        if (Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            telemetry.RecordAuthFailure(reason);
        }
    }
}
