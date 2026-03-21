USE [ClinicalTrialsMcp];
GO

/*
Local development seed
Raw API key: clinical-trials-local-dev-key
Header name: X-Api-Key
*/

IF NOT EXISTS (
    SELECT 1
    FROM dbo.McpClients
    WHERE ClientCode = N'local-dev-client')
BEGIN
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
        'A6C40F69-9E58-4D40-87E2-6E0D8A0A5D11',
        N'local-dev-client',
        N'Local Development Client',
        1,
        NULL,
        NULL,
        NULL,
        SYSUTCDATETIME(),
        SYSUTCDATETIME(),
        NULL
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM dbo.McpClientApiKeys
    WHERE KeyPrefix = N'52A73A82AE88D094')
BEGIN
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
        'CB93324E-0FCE-47D7-9AAB-E9FA482C9A12',
        'A6C40F69-9E58-4D40-87E2-6E0D8A0A5D11',
        N'local-dev',
        N'52A73A82AE88D094',
        N'PBKDF2-SHA512',
        120000,
        N'RzS/gNXMloLMB6lrsiKBoqE1jEcNu8XqsTAJvhlgPiY=',
        N'5xfngOETPcKae6S+XxqBzvwuevAekSPAjaM6Tx53mvE=',
        1,
        SYSUTCDATETIME(),
        NULL,
        NULL,
        NULL
    );
END;
GO
