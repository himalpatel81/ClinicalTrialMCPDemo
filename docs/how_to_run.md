# How To Run

This document explains how to run the ClinicalTrials REST API, the MCP server, the admin CLI, and the automated tests from this repository.

Local runtime remains a required use case. The MCP server stays runnable on a developer machine without Azure Key Vault, Azure SQL, Azure Redis, or Azure Monitor.

## Prerequisites

- .NET SDK `10.x`
- internet access to reach `https://clinicaltrials.gov/`
- local SQL Server instance already running
- PowerShell or another terminal that can run `dotnet`

## Repository Root

All commands below assume you are in the repository root:

```powershell
cd E:\GitHimal\ClinicalTrialMCPDemo
```

## Projects In This Repo

- `ClinicalTrials.Api`
  - REST API host
- `ClinicalTrials.Mcp`
  - HTTP MCP host
- `ClinicalTrials.Admin`
  - admin CLI for client and API key lifecycle
- `ClinicalTrials.Shared`
  - shared ClinicalTrials.gov lookup service used by the REST API
- `ClinicalTrials.Tests`
  - automated tests

## Build Everything

```powershell
dotnet build .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

## Prepare Local SQL

Run these checked-in SQL scripts against your local SQL Server instance:

1. [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql)
2. [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql)

The seed script inserts the local development client and this raw API key:

```text
clinical-trials-local-dev-key
```

## Set The MCP Client Registry Connection String

The MCP host now requires a SQL connection string for the client registry.

Set it in the current PowerShell session before starting the MCP host:

```powershell
$env:ConnectionStrings__ClientRegistry = "Server=localhost,1433;Database=ClinicalTrialsMcp;User Id=sa;Password=<your-password>;Encrypt=False;TrustServerCertificate=True"
```

You can also set the same value with `.NET user-secrets` for the MCP project if you prefer.

## Run The REST API

```powershell
dotnet run --project .\ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Default development URLs:

- `http://localhost:5010`
- `https://localhost:7001`

Verify the REST API:

```powershell
Invoke-WebRequest http://localhost:5010/api/studies/NCT04924608
```

Expected result:

- HTTP `200 OK`
- JSON study payload returned from ClinicalTrials.gov

## Run The MCP Server

The MCP host now routes study lookups through `ClinicalTrials.Api` and authenticates callers against the SQL-backed client registry.

For local development, start the REST API before starting the MCP server.

Terminal 1:

```powershell
dotnet run --project .\ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Terminal 2:

```powershell
dotnet run --project .\ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

Default development URLs:

- `http://localhost:5011`
- `https://localhost:7002`

Important endpoints:

- MCP endpoint: `http://localhost:5011/mcp`
- liveness endpoint: `http://localhost:5011/health/live`
- readiness endpoint: `http://localhost:5011/health/ready`

Phase 3 defaults:

- study-service resilience with retry, timeout, and circuit breaker
- MCP request timeout: `30` seconds
- MCP max request body size: `1048576` bytes
- correlation header: `X-Correlation-Id`

## Local MCP Authentication

The MCP endpoint requires an API key.

Local development uses this seeded key after you run the SQL seed script:

```text
clinical-trials-local-dev-key
```

Header name:

```text
X-Api-Key
```

## Local Cache And Rate Limiting

Local development uses:

- SQL Server for the client registry
- in-memory study lookup caching
- in-memory rate limiting

Local development does not require local Redis.

## Optional Telemetry Configuration

Local export is optional.

Enable console OpenTelemetry output:

```powershell
$env:Telemetry__Console__Enabled = "true"
```

Enable Azure Monitor / Application Insights export:

```powershell
$env:Telemetry__ApplicationInsights__ConnectionString = "<connection-string>"
```

If neither is set, local development still works and structured logs remain available through the normal host log output.

## Verify The MCP Server

Check the health endpoints:

```powershell
Invoke-WebRequest http://localhost:5011/health/live
Invoke-WebRequest http://localhost:5011/health/ready
```

Expected result:

- `/health/live` returns HTTP `200 OK` with body `healthy`
- `/health/ready` returns HTTP `200 OK` with a JSON readiness report when local SQL is reachable
- the response includes `X-Correlation-Id`

Important:

- `/health/ready` now validates:
  - MCP host configuration
  - client-registry SQL connectivity
  - Redis connectivity if Redis-backed providers are enabled
  - secrets-provider configuration
- `/health/ready` still does not prove `ClinicalTrials.Api` is currently reachable

## Connect An MCP Client

The repository includes a workspace MCP configuration at:

```text
.vscode\mcp.json
```

Current server entry:

```json
{
  "servers": {
    "clinical-trials-mcp": {
      "type": "http",
      "url": "http://localhost:5011/mcp",
      "headers": {
        "X-Api-Key": "clinical-trials-local-dev-key"
      }
    }
  },
  "inputs": []
}
```

Once the MCP server is running, an MCP-capable client can call:

```text
get_study_by_nct_id
```

Example intent:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected result:

- the client sends `X-Api-Key`
- the tool returns a clean summary in visible content
- the tool returns the raw study JSON as structured content
- the MCP host calls `ClinicalTrials.Api`
- the REST API then calls ClinicalTrials.gov

## Use The Admin CLI

The admin CLI project is:

```text
ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj
```

Example commands:

Create a client:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- create-client --connection-string "$env:ConnectionStrings__ClientRegistry" --client-code demo-client --display-name "Demo Client"
```

Issue an API key:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- issue-key --connection-string "$env:ConnectionStrings__ClientRegistry" --client-code demo-client --key-label primary
```

Rotate an API key:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- rotate-key --connection-string "$env:ConnectionStrings__ClientRegistry" --client-code demo-client --key-label rotated
```

Revoke an API key:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- revoke-key --connection-string "$env:ConnectionStrings__ClientRegistry" --api-key-id <guid>
```

Deactivate a client:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- deactivate-client --connection-string "$env:ConnectionStrings__ClientRegistry" --client-code demo-client
```

## Run Automated Tests

```powershell
dotnet test .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

The test suite covers:

- shared lookup service behavior
- REST API integration behavior
- MCP tool behavior
- SQL-backed API key hashing and verification behavior
- cached study lookup behavior
- admin service behavior
- MCP host integration behavior
- rate limiting behavior
- readiness dependency behavior
- request-size protection behavior
- correlation-header behavior

## Common Troubleshooting

### MCP Host Fails To Start

Checks:

- verify `ConnectionStrings__ClientRegistry` is set
- verify `StudyService:BaseUrl` points to the REST API host
- verify cache and rate limit providers are `Memory` in local development

### MCP Returns `401 Unauthorized`

Checks:

- verify the SQL seed script ran successfully
- verify the client is sending `X-Api-Key`
- verify the local key value is `clinical-trials-local-dev-key`

### MCP Returns `403 Forbidden`

Checks:

- verify the client record and key record are still active in the client registry
- verify the key has not been revoked or expired

### MCP Returns `429 Too Many Requests`

Checks:

- retry after the current rate-limit window
- check whether the local client has a permit limit override in the client registry

### MCP Returns `413 Payload Too Large`

Checks:

- verify the MCP request body is below `RequestProtection:MaxRequestBodySizeBytes`
- verify the client is not sending an unexpectedly large MCP payload

### MCP Returns `502 Bad Gateway`

Checks:

- verify `ClinicalTrials.Api` is running on `http://localhost:5010`
- verify the REST API can answer `http://localhost:5010/api/studies/NCT04924608`

### MCP Returns `504 Gateway Timeout`

Checks:

- verify `ClinicalTrials.Api` is responsive
- verify the study-service call is not exceeding the configured resilience timeout budget
- review `StudyService:Resilience` settings if you changed them locally

## Related Documents

- `docs\index.md`
- `docs\production-readiness.md`
- `docs\mcp-server.md`
- `docs\mcp_tech.md`
- `docs\mcp_func.md`
- `docs\operations-runbook.md`
- `docs\studies-controller-endpoints.md`
