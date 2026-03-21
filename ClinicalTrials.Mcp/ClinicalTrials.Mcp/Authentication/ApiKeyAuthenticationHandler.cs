using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Authentication;

internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ApiKeyAuthenticationSettings> settings)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private readonly ApiKeyAuthenticationSettings settings = settings.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(this.settings.HeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (headerValues.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.Fail("A single API key header value is required."));
        }

        var providedApiKey = headerValues[0];
        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("The API key header value cannot be empty."));
        }

        var configuredClient = this.settings.Clients.FirstOrDefault(client => ApiKeysMatch(client.ApiKey, providedApiKey));
        if (configuredClient is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("The supplied API key is invalid."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, configuredClient.ClientId),
            new Claim(ClaimTypes.Name, configuredClient.ClientId),
            new Claim(ApiKeyAuthenticationDefaults.ClientIdClaimType, configuredClient.ClientId),
            new Claim(ApiKeyAuthenticationDefaults.ClientActiveClaimType, configuredClient.Enabled.ToString())
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationDefaults.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationDefaults.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool ApiKeysMatch(string configuredApiKey, string providedApiKey)
    {
        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);

        return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }
}
