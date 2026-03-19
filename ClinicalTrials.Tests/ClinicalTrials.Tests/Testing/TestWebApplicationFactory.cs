using ClinicalTrials.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicalTrials.Tests.Testing;

internal sealed class TestWebApplicationFactory<TEntryPoint>(HttpMessageHandler handler)
    : WebApplicationFactory<TEntryPoint> where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("ClinicalTrials:BaseUrl", "https://clinicaltrials.test/")
            ]);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(ClinicalTrialsServiceCollectionExtensions.ClinicalTrialsGovClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        });
    }
}
