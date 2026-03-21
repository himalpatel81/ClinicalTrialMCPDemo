namespace ClinicalTrials.Mcp.Authentication;

internal static class ApiKeyAuthenticationDefaults
{
    public const string SchemeName = "ApiKey";
    public const string ActiveClientPolicyName = "ActiveApiKeyClient";
    public const string ClientIdClaimType = "clinicaltrials.client_id";
    public const string ClientActiveClaimType = "clinicaltrials.client_active";
    public const string ApiKeyActiveClaimType = "clinicaltrials.api_key_active";
    public const string PermitLimitClaimType = "clinicaltrials.permit_limit";
}
