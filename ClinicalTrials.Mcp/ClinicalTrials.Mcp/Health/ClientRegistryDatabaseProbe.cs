using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ClinicalTrials.Mcp.Data;

namespace ClinicalTrials.Mcp.Health;

internal sealed class ClientRegistryDatabaseProbe(IOptions<ClientRegistryDatabaseSettings> settings) : IClientRegistryDatabaseProbe
{
    private readonly string connectionString = settings.Value.ConnectionString;

    public async Task ProbeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT 1;", connection);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
