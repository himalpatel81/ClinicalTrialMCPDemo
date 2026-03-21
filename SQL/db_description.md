# Database Description

This document describes the SQL schema created by [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql) and the local development seed data added by [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql).

The database name is `ClinicalTrialsMcp`. It stores MCP client registry data used for:

- client identification
- API key validation
- client activation and revocation
- client-specific policy overrides such as rate limit and cache settings

## Tables

### `dbo.McpClients`

Purpose: Stores one record per approved MCP client application or consumer.

| Column | Type | Null | Description |
| --- | --- | --- | --- |
| `ClientId` | `UNIQUEIDENTIFIER` | No | Primary key for the client record. |
| `ClientCode` | `NVARCHAR(100)` | No | Stable unique code for the client. Used as a business identifier. |
| `DisplayName` | `NVARCHAR(200)` | No | Human-readable client name for administration and reporting. |
| `IsActive` | `BIT` | No | Indicates whether the client is currently allowed to authenticate. Default is `1`. |
| `PermitLimitOverride` | `INT` | Yes | Optional per-client override for request/rate-limit policy. |
| `CacheTtlSecondsOverride` | `INT` | Yes | Optional per-client override for successful response cache TTL in seconds. |
| `NegativeCacheTtlSecondsOverride` | `INT` | Yes | Optional per-client override for negative-cache TTL in seconds. |
| `CreatedUtc` | `DATETIMEOFFSET(0)` | No | UTC timestamp when the client record was created. |
| `UpdatedUtc` | `DATETIMEOFFSET(0)` | No | UTC timestamp when the client record was last updated. |
| `DeactivatedUtc` | `DATETIMEOFFSET(0)` | Yes | UTC timestamp when the client was deactivated, if applicable. |

Constraints:

- `PK_McpClients`
  - primary key on `ClientId`
- `UQ_McpClients_ClientCode`
  - unique constraint on `ClientCode`

Notes:

- A client may have multiple API keys over time.
- The override columns allow client-specific operational behavior without changing the tool contract.

### `dbo.McpClientApiKeys`

Purpose: Stores hashed API keys for MCP clients. Raw API keys are not stored in the database.

| Column | Type | Null | Description |
| --- | --- | --- | --- |
| `ApiKeyId` | `UNIQUEIDENTIFIER` | No | Primary key for the API key record. |
| `ClientId` | `UNIQUEIDENTIFIER` | No | Foreign key to the owning client in `dbo.McpClients`. |
| `KeyLabel` | `NVARCHAR(100)` | No | Human-readable label for the key, such as `local-dev` or `prod-primary`. |
| `KeyPrefix` | `NVARCHAR(32)` | No | Non-secret prefix used for lookup and operations. This is unique per key. |
| `HashAlgorithm` | `NVARCHAR(50)` | No | Name of the hashing algorithm used to derive the stored hash. |
| `HashIterations` | `INT` | No | Iteration count used by the hash algorithm. |
| `SaltBase64` | `NVARCHAR(256)` | No | Base64-encoded cryptographic salt used when hashing the raw API key. |
| `HashBase64` | `NVARCHAR(256)` | No | Base64-encoded derived hash of the API key. |
| `IsActive` | `BIT` | No | Indicates whether this key is currently valid for authentication. Default is `1`. |
| `CreatedUtc` | `DATETIMEOFFSET(0)` | No | UTC timestamp when the key record was created. |
| `ExpiresUtc` | `DATETIMEOFFSET(0)` | Yes | Optional UTC timestamp when the key expires. |
| `RevokedUtc` | `DATETIMEOFFSET(0)` | Yes | UTC timestamp when the key was revoked, if applicable. |
| `ReplacedByApiKeyId` | `UNIQUEIDENTIFIER` | Yes | Optional self-reference to the replacement key created during key rotation. |

Constraints:

- `PK_McpClientApiKeys`
  - primary key on `ApiKeyId`
- `FK_McpClientApiKeys_Client`
  - foreign key from `ClientId` to `dbo.McpClients(ClientId)`
- `FK_McpClientApiKeys_ReplacedBy`
  - self-referencing foreign key from `ReplacedByApiKeyId` to `dbo.McpClientApiKeys(ApiKeyId)`

Indexes:

- `IX_McpClientApiKeys_KeyPrefix`
  - unique nonclustered index on `KeyPrefix`
- `IX_McpClientApiKeys_ClientId_IsActive`
  - nonclustered index on `ClientId, IsActive`

Notes:

- `KeyPrefix` is used to find candidate keys efficiently without storing the full secret.
- `SaltBase64`, `HashBase64`, `HashAlgorithm`, and `HashIterations` together define the stored credential material needed for verification.
- `ReplacedByApiKeyId` supports auditability during key rotation.

## Relationships

- One row in `dbo.McpClients` can be associated with many rows in `dbo.McpClientApiKeys`.
- Each row in `dbo.McpClientApiKeys` belongs to exactly one client.
- A key can optionally reference another key that replaced it.

## Seed Data

The local development seed script inserts:

- one client record with `ClientCode = local-dev-client`
- one API key record with `KeyLabel = local-dev`

Local development values from the seed script:

- header name: `X-Api-Key`
- raw API key: `clinical-trials-local-dev-key`
- key prefix: `52A73A82AE88D094`
- hash algorithm: `PBKDF2-SHA512`
- hash iterations: `120000`

This seed data is intended for local development only.
