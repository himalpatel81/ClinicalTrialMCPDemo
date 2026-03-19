using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicalTrials.Shared;

public static class ClinicalTrialsServiceCollectionExtensions
{
    public const string ClinicalTrialsGovClientName = "ClinicalTrialsGov";

    public static IServiceCollection AddClinicalTrialsStudyLookup(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var baseUrl = configuration["ClinicalTrials:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration 'ClinicalTrials:BaseUrl' is required.");

        services.AddHttpClient(ClinicalTrialsGovClientName, client =>
        {
            client.BaseAddress = new Uri(baseUrl);
        });

        services.AddScoped<IStudyLookupService, StudyLookupService>();

        return services;
    }
}
