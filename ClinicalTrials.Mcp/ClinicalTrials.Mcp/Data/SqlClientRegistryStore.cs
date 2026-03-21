using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ClinicalTrials.Mcp.Data;

public sealed class SqlClientRegistryStore(IOptions<ClientRegistryDatabaseSettings> settings) : IClientRegistryStore
{
    private readonly string connectionString = settings.Value.ConnectionString;

    public async Task<IReadOnlyList<ApiKeyAuthenticationRecord>> GetAuthenticationCandidatesAsync(string keyPrefix, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                c.ClientId,
                c.ClientCode,
                c.DisplayName,
                c.IsActive,
                c.PermitLimitOverride,
                k.ApiKeyId,
                k.KeyLabel,
                k.KeyPrefix,
                k.HashAlgorithm,
                k.HashIterations,
                k.SaltBase64,
                k.HashBase64,
                k.IsActive,
                k.ExpiresUtc,
                k.RevokedUtc
            FROM dbo.McpClients c
            INNER JOIN dbo.McpClientApiKeys k ON c.ClientId = k.ClientId
            WHERE k.KeyPrefix = @KeyPrefix;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@KeyPrefix", keyPrefix);

        var records = new List<ApiKeyAuthenticationRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(
                new ApiKeyAuthenticationRecord(
                    ClientId: reader.GetGuid(0),
                    ClientCode: reader.GetString(1),
                    DisplayName: reader.GetString(2),
                    ClientIsActive: reader.GetBoolean(3),
                    PermitLimitOverride: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    ApiKeyId: reader.GetGuid(5),
                    KeyLabel: reader.GetString(6),
                    KeyPrefix: reader.GetString(7),
                    HashAlgorithm: reader.GetString(8),
                    HashIterations: reader.GetInt32(9),
                    SaltBase64: reader.GetString(10),
                    HashBase64: reader.GetString(11),
                    ApiKeyIsActive: reader.GetBoolean(12),
                    ExpiresUtc: reader.IsDBNull(13) ? null : reader.GetDateTimeOffset(13),
                    RevokedUtc: reader.IsDBNull(14) ? null : reader.GetDateTimeOffset(14)));
        }

        return records;
    }

    public async Task<ClientRegistryClient?> GetClientByCodeAsync(string clientCode, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                ClientId,
                ClientCode,
                DisplayName,
                IsActive,
                PermitLimitOverride,
                CacheTtlSecondsOverride,
                NegativeCacheTtlSecondsOverride,
                CreatedUtc,
                UpdatedUtc,
                DeactivatedUtc
            FROM dbo.McpClients
            WHERE ClientCode = @ClientCode;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientCode", clientCode);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ClientRegistryClient(
            ClientId: reader.GetGuid(0),
            ClientCode: reader.GetString(1),
            DisplayName: reader.GetString(2),
            IsActive: reader.GetBoolean(3),
            PermitLimitOverride: reader.IsDBNull(4) ? null : reader.GetInt32(4),
            CacheTtlSecondsOverride: reader.IsDBNull(5) ? null : reader.GetInt32(5),
            NegativeCacheTtlSecondsOverride: reader.IsDBNull(6) ? null : reader.GetInt32(6),
            CreatedUtc: reader.GetDateTimeOffset(7),
            UpdatedUtc: reader.GetDateTimeOffset(8),
            DeactivatedUtc: reader.IsDBNull(9) ? null : reader.GetDateTimeOffset(9));
    }

    public async Task CreateClientAsync(ClientRegistryClient client, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO dbo.McpClients
            (
                ClientId,
                ClientCode,
                DisplayName,
                IsActive,
                PermitLimitOverride,
                CacheTtlSecondsOverride,
                NegativeCacheTtlSecondsOverride,
                CreatedUtc,
                UpdatedUtc,
                DeactivatedUtc
            )
            VALUES
            (
                @ClientId,
                @ClientCode,
                @DisplayName,
                @IsActive,
                @PermitLimitOverride,
                @CacheTtlSecondsOverride,
                @NegativeCacheTtlSecondsOverride,
                @CreatedUtc,
                @UpdatedUtc,
                @DeactivatedUtc
            );
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddClientParameters(command, client);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClientRegistryApiKey>> GetApiKeysByClientCodeAsync(string clientCode, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT
                k.ApiKeyId,
                k.ClientId,
                c.ClientCode,
                k.KeyLabel,
                k.KeyPrefix,
                k.HashAlgorithm,
                k.HashIterations,
                k.SaltBase64,
                k.HashBase64,
                k.IsActive,
                k.CreatedUtc,
                k.ExpiresUtc,
                k.RevokedUtc,
                k.ReplacedByApiKeyId
            FROM dbo.McpClientApiKeys k
            INNER JOIN dbo.McpClients c ON c.ClientId = k.ClientId
            WHERE c.ClientCode = @ClientCode
            ORDER BY k.CreatedUtc DESC;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientCode", clientCode);

        var keys = new List<ClientRegistryApiKey>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(
                new ClientRegistryApiKey(
                    ApiKeyId: reader.GetGuid(0),
                    ClientId: reader.GetGuid(1),
                    ClientCode: reader.GetString(2),
                    KeyLabel: reader.GetString(3),
                    KeyPrefix: reader.GetString(4),
                    HashAlgorithm: reader.GetString(5),
                    HashIterations: reader.GetInt32(6),
                    SaltBase64: reader.GetString(7),
                    HashBase64: reader.GetString(8),
                    IsActive: reader.GetBoolean(9),
                    CreatedUtc: reader.GetDateTimeOffset(10),
                    ExpiresUtc: reader.IsDBNull(11) ? null : reader.GetDateTimeOffset(11),
                    RevokedUtc: reader.IsDBNull(12) ? null : reader.GetDateTimeOffset(12),
                    ReplacedByApiKeyId: reader.IsDBNull(13) ? null : reader.GetGuid(13)));
        }

        return keys;
    }

    public async Task AddApiKeyAsync(ClientRegistryApiKey apiKey, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO dbo.McpClientApiKeys
            (
                ApiKeyId,
                ClientId,
                KeyLabel,
                KeyPrefix,
                HashAlgorithm,
                HashIterations,
                SaltBase64,
                HashBase64,
                IsActive,
                CreatedUtc,
                ExpiresUtc,
                RevokedUtc,
                ReplacedByApiKeyId
            )
            VALUES
            (
                @ApiKeyId,
                @ClientId,
                @KeyLabel,
                @KeyPrefix,
                @HashAlgorithm,
                @HashIterations,
                @SaltBase64,
                @HashBase64,
                @IsActive,
                @CreatedUtc,
                @ExpiresUtc,
                @RevokedUtc,
                @ReplacedByApiKeyId
            );
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ApiKeyId", apiKey.ApiKeyId);
        command.Parameters.AddWithValue("@ClientId", apiKey.ClientId);
        command.Parameters.AddWithValue("@KeyLabel", apiKey.KeyLabel);
        command.Parameters.AddWithValue("@KeyPrefix", apiKey.KeyPrefix);
        command.Parameters.AddWithValue("@HashAlgorithm", apiKey.HashAlgorithm);
        command.Parameters.AddWithValue("@HashIterations", apiKey.HashIterations);
        command.Parameters.AddWithValue("@SaltBase64", apiKey.SaltBase64);
        command.Parameters.AddWithValue("@HashBase64", apiKey.HashBase64);
        command.Parameters.AddWithValue("@IsActive", apiKey.IsActive);
        command.Parameters.AddWithValue("@CreatedUtc", apiKey.CreatedUtc);
        command.Parameters.AddWithValue("@ExpiresUtc", (object?)apiKey.ExpiresUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@RevokedUtc", (object?)apiKey.RevokedUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("@ReplacedByApiKeyId", (object?)apiKey.ReplacedByApiKeyId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RevokeApiKeyAsync(Guid apiKeyId, DateTimeOffset revokedUtc, Guid? replacedByApiKeyId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE dbo.McpClientApiKeys
            SET
                IsActive = 0,
                RevokedUtc = @RevokedUtc,
                ReplacedByApiKeyId = @ReplacedByApiKeyId
            WHERE ApiKeyId = @ApiKeyId;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ApiKeyId", apiKeyId);
        command.Parameters.AddWithValue("@RevokedUtc", revokedUtc);
        command.Parameters.AddWithValue("@ReplacedByApiKeyId", (object?)replacedByApiKeyId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeactivateClientAsync(string clientCode, DateTimeOffset deactivatedUtc, CancellationToken cancellationToken)
    {
        const string sql =
            """
            UPDATE dbo.McpClients
            SET
                IsActive = 0,
                UpdatedUtc = @DeactivatedUtc,
                DeactivatedUtc = @DeactivatedUtc
            WHERE ClientCode = @ClientCode;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ClientCode", clientCode);
        command.Parameters.AddWithValue("@DeactivatedUtc", deactivatedUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqlConnection CreateConnection() => new(connectionString);

    private static void AddClientParameters(SqlCommand command, ClientRegistryClient client)
    {
        command.Parameters.AddWithValue("@ClientId", client.ClientId);
        command.Parameters.AddWithValue("@ClientCode", client.ClientCode);
        command.Parameters.AddWithValue("@DisplayName", client.DisplayName);
        command.Parameters.AddWithValue("@IsActive", client.IsActive);
        command.Parameters.AddWithValue("@PermitLimitOverride", (object?)client.PermitLimitOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@CacheTtlSecondsOverride", (object?)client.CacheTtlSecondsOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@NegativeCacheTtlSecondsOverride", (object?)client.NegativeCacheTtlSecondsOverride ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedUtc", client.CreatedUtc);
        command.Parameters.AddWithValue("@UpdatedUtc", client.UpdatedUtc);
        command.Parameters.AddWithValue("@DeactivatedUtc", (object?)client.DeactivatedUtc ?? DBNull.Value);
    }
}
