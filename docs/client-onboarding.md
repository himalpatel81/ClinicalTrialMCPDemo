# Client Onboarding

This document describes the current process for onboarding a new MCP client to the `ClinicalTrials.Mcp` service.

## Overview

Client onboarding is currently an admin-driven process.

There is no public self-service registration endpoint. A new client is onboarded by:

1. preparing the client registry database
2. creating a client record
3. issuing an API key
4. securely sharing the raw API key with the client
5. having the client configure its MCP connection
6. validating that the client can call the MCP tool successfully

## Important Clarification

The admin CLI does **not** create the database or tables.

You must create the database schema first by running the checked-in SQL scripts:

- [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql)
- optionally [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql) for local development only

What the admin CLI **does** create:

- a new row in `dbo.McpClients` when you run `create-client`
- a new row in `dbo.McpClientApiKeys` when you run `issue-key`

If the database or tables do not already exist, the CLI commands will fail.

Schema details are documented in [db_description.md](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/db_description.md).

## Prerequisites

- the `ClinicalTrialsMcp` database and tables already exist
- you have a valid SQL connection string for the client registry
- you have access to run the admin CLI from this repository

Admin CLI project:

- [ClinicalTrials.Admin.csproj](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Admin/ClinicalTrials.Admin/ClinicalTrials.Admin.csproj)

## Step 1: Prepare The Database

Run the schema script against the target SQL Server database:

```powershell
sqlcmd -S <server> -d master -i .\SQL\001_create_client_registry.sql
```

For local development only, you can also run the local seed script:

```powershell
sqlcmd -S <server> -d ClinicalTrialsMcp -i .\SQL\002_seed_local_dev_client.sql
```

## Step 2: Set The Connection String

You can either pass the connection string directly on each command with `--connection-string`, or set the environment variable:

```powershell
$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING = "Server=localhost,1433;Database=ClinicalTrialsMcp;User Id=sa;Password=<your-password>;Encrypt=False;TrustServerCertificate=True"
```

## Step 3: Create The Client

Use `create-client` to insert a new record into `dbo.McpClients`.

Example:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- create-client --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code demo-client --display-name "Demo Client"
```

Optional policy overrides can also be set at creation time:

- `--permit-limit <int>`
- `--cache-ttl-seconds <int>`
- `--negative-cache-ttl-seconds <int>`

Example with overrides:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- create-client --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code partner-a --display-name "Partner A" --permit-limit 60 --cache-ttl-seconds 120 --negative-cache-ttl-seconds 30
```

Expected result:

- one new row in `dbo.McpClients`
- `IsActive = 1`
- a generated `ClientId`

## Step 4: Issue The First API Key

Use `issue-key` to insert a hashed API key record into `dbo.McpClientApiKeys`.

Example:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- issue-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code demo-client --key-label primary
```

Optional expiry:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- issue-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code demo-client --key-label primary --expires-utc 2026-12-31T23:59:59Z
```

Expected result:

- one new row in `dbo.McpClientApiKeys`
- a printed `API key ID`
- a printed `Key prefix`
- a printed `Raw API key`

Important:

- the raw API key is shown once in the CLI output
- the database stores only the hashed form, not the raw secret
- you must capture and share the raw key securely when it is issued

## Step 5: Share The Client Configuration

Provide the client with:

- the MCP endpoint URL
- the `X-Api-Key` header name
- the raw API key value

Example Roo Code configuration:

```json
{
  "mcpServers": {
    "clinical-trials-mcp": {
      "type": "streamable-http",
      "url": "https://<your-host>/mcp",
      "headers": {
        "X-Api-Key": "<raw-api-key>"
      }
    }
  }
}
```

## Step 6: Validate The Client

Have the client connect and call:

```text
get_study_by_nct_id
```

Example prompt:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected result:

- the MCP request is authenticated
- the tool call succeeds
- the client receives the visible summary plus structured JSON

## Key Rotation

Use `rotate-key` when you want to issue a replacement key and revoke prior active keys for the client.

Example:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- rotate-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code demo-client --key-label rotated
```

Effect:

- a new active API key row is created
- older active keys for that client are revoked
- the old rows are linked through `ReplacedByApiKeyId`

## Key Revocation

Use `revoke-key` to revoke one specific key.

Example:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- revoke-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --api-key-id <guid>
```

Effect:

- the specified key can no longer authenticate

## Client Deactivation

Use `deactivate-client` to disable the client entirely.

Example:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- deactivate-client --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code demo-client
```

Effect:

- the client record is marked inactive
- authentication for that client is blocked

## Failure Modes

- if the DB schema does not exist, admin commands fail
- if `ClientCode` already exists, `create-client` fails
- if the target client does not exist, `issue-key` and `rotate-key` fail
- if the client is inactive, `issue-key` and `rotate-key` fail
- if a key is revoked, expired, or inactive, authentication fails

## Related Documents

- [how_to_run.md](/e:/GitHimal/ClinicalTrialMCPDemo/docs/how_to_run.md)
- [mcp-server.md](/e:/GitHimal/ClinicalTrialMCPDemo/docs/mcp-server.md)
- [mcp_tech.md](/e:/GitHimal/ClinicalTrialMCPDemo/docs/mcp_tech.md)
- [db_description.md](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/db_description.md)
