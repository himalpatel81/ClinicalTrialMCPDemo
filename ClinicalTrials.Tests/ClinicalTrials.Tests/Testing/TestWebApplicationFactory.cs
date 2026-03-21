using ClinicalTrials.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicalTrials.Tests.Testing;

internal sealed class TestWebApplicationFactory<TEntryPoint>(
    HttpMessageHandler handler,
    IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
{
    public const string DefaultApiKey = "integration-test-api-key";
    public const string ApiKeyHeaderName = "X-Api-Key";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ClinicalTrials:BaseUrl"] = "https://clinicaltrials.test/",
                ["StudyService:BaseUrl"] = "https://clinicaltrials.api.test/",
                ["StudyService:StudyLookupPathTemplate"] = "api/studies/{nctId}",
                ["Authentication:ApiKeys:HeaderName"] = ApiKeyHeaderName,
                ["Authentication:ApiKeys:Clients:0:ClientId"] = "integration-test-client",
                ["Authentication:ApiKeys:Clients:0:ApiKey"] = DefaultApiKey,
                ["Authentication:ApiKeys:Clients:0:Enabled"] = "true"
            };

            if (additionalConfiguration is not null)
            {
                foreach (var entry in additionalConfiguration)
                {
                    settings[entry.Key] = entry.Value;
                }
            }

            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(ClinicalTrialsServiceCollectionExtensions.ClinicalTrialsGovClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
            services.AddHttpClient(ClinicalTrials.Mcp.Services.StudyServiceHttpClient.ClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyHeaderName, DefaultApiKey);

        return client;
    }
}
