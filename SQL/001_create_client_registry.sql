IF DB_ID(N'ClinicalTrialsMcp') IS NULL
BEGIN
    CREATE DATABASE [ClinicalTrialsMcp];
END;
GO

USE [ClinicalTrialsMcp];
GO

IF OBJECT_ID(N'dbo.McpClients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.McpClients
    (
        ClientId UNIQUEIDENTIFIER NOT NULL,
        ClientCode NVARCHAR(100) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_McpClients_IsActive DEFAULT (1),
        PermitLimitOverride INT NULL,
        CacheTtlSecondsOverride INT NULL,
        NegativeCacheTtlSecondsOverride INT NULL,
        CreatedUtc DATETIMEOFFSET(0) NOT NULL,
        UpdatedUtc DATETIMEOFFSET(0) NOT NULL,
        DeactivatedUtc DATETIMEOFFSET(0) NULL,
        CONSTRAINT PK_McpClients PRIMARY KEY CLUSTERED (ClientId),
        CONSTRAINT UQ_McpClients_ClientCode UNIQUE (ClientCode)
    );
END;
GO

IF OBJECT_ID(N'dbo.McpClientApiKeys', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.McpClientApiKeys
    (
        ApiKeyId UNIQUEIDENTIFIER NOT NULL,
        ClientId UNIQUEIDENTIFIER NOT NULL,
        KeyLabel NVARCHAR(100) NOT NULL,
        KeyPrefix NVARCHAR(32) NOT NULL,
        HashAlgorithm NVARCHAR(50) NOT NULL,
        HashIterations INT NOT NULL,
        SaltBase64 NVARCHAR(256) NOT NULL,
        HashBase64 NVARCHAR(256) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_McpClientApiKeys_IsActive DEFAULT (1),
        CreatedUtc DATETIMEOFFSET(0) NOT NULL,
        ExpiresUtc DATETIMEOFFSET(0) NULL,
        RevokedUtc DATETIMEOFFSET(0) NULL,
        ReplacedByApiKeyId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_McpClientApiKeys PRIMARY KEY CLUSTERED (ApiKeyId),
        CONSTRAINT FK_McpClientApiKeys_Client FOREIGN KEY (ClientId) REFERENCES dbo.McpClients (ClientId)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_McpClientApiKeys_ReplacedBy'
        AND parent_object_id = OBJECT_ID(N'dbo.McpClientApiKeys'))
BEGIN
    ALTER TABLE dbo.McpClientApiKeys
    ADD CONSTRAINT FK_McpClientApiKeys_ReplacedBy
        FOREIGN KEY (ReplacedByApiKeyId) REFERENCES dbo.McpClientApiKeys (ApiKeyId);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_McpClientApiKeys_KeyPrefix'
        AND object_id = OBJECT_ID(N'dbo.McpClientApiKeys'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_McpClientApiKeys_KeyPrefix
        ON dbo.McpClientApiKeys (KeyPrefix);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_McpClientApiKeys_ClientId_IsActive'
        AND object_id = OBJECT_ID(N'dbo.McpClientApiKeys'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_McpClientApiKeys_ClientId_IsActive
        ON dbo.McpClientApiKeys (ClientId, IsActive);
END;
GO
