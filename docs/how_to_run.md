# How To Run

This document explains how to run the ClinicalTrials REST API, the MCP server, and the automated tests from this repository.

Local runtime remains a required use case. The MCP server must stay runnable on a developer machine without Azure Key Vault, Azure SQL, Azure Redis, or Azure Monitor.

## Prerequisites

- .NET SDK `10.x`
- internet access to reach `https://clinicaltrials.gov/`
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
- `ClinicalTrials.Shared`
  - shared ClinicalTrials.gov lookup service
- `ClinicalTrials.Tests`
  - automated tests

## Build Everything

```powershell
dotnet build .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

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

The MCP host now routes study lookups through `ClinicalTrials.Api`.

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

## Local MCP Authentication

The MCP endpoint now requires an API key.

Local development uses this seeded key:

```text
clinical-trials-local-dev-key
```

Header name:

```text
X-Api-Key
```

This key is for local development only. Do not reuse it in any shared or production environment.

## Verify The MCP Server

Check the health endpoints:

```powershell
Invoke-WebRequest http://localhost:5011/health/live
Invoke-WebRequest http://localhost:5011/health/ready
```

Expected result:

- `/health/live` returns HTTP `200 OK` with body `healthy`
- `/health/ready` returns HTTP `200 OK`

Important:

- `/health/ready` validates MCP host configuration only
- a successful health check does not prove the REST API is currently reachable

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
- the lookup request is sent from MCP to `ClinicalTrials.Api`, which then calls ClinicalTrials.gov

## Run Automated Tests

```powershell
dotnet test .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

The test suite covers:

- shared lookup service behavior
- REST API integration behavior
- MCP tool behavior
- MCP host integration behavior
- authenticated and unauthenticated MCP access

## Run API And MCP Together

Open two terminals from the repo root.

Terminal 1:

```powershell
dotnet run --project .\ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Terminal 2:

```powershell
dotnet run --project .\ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

Then use:

- REST API on `http://localhost:5010`
- MCP on `http://localhost:5011/mcp`

## Common Troubleshooting

### ClinicalTrials.gov Cannot Be Reached

Symptoms:

- REST API returns `502` or `504`
- MCP tool returns structured error with `502` or `504`

Checks:

- verify internet access
- verify `https://clinicaltrials.gov/` is reachable
- verify `ClinicalTrials:BaseUrl` is present in the REST API configuration

### MCP Client Gets `401 Unauthorized`

Checks:

- verify the client is sending `X-Api-Key`
- verify the local key value is `clinical-trials-local-dev-key`
- verify the MCP server is running in `Development`

### MCP Client Cannot Connect

Checks:

- verify the MCP server is running
- verify `.vscode\mcp.json` points to `http://localhost:5011/mcp`
- verify `.vscode\mcp.json` includes the `X-Api-Key` header
- verify `http://localhost:5011/health/live` responds successfully
- verify `http://localhost:5011/health/ready` responds successfully

### MCP Returns `502 Bad Gateway`

Checks:

- verify `ClinicalTrials.Api` is running on `http://localhost:5010`
- verify `StudyService:BaseUrl` points to the correct REST API host
- verify the REST API can answer `http://localhost:5010/api/studies/NCT04924608`

## Related Documents

- `docs\index.md`
- `docs\production-readiness.md`
- `docs\mcp-server.md`
- `docs\mcp_tech.md`
- `docs\mcp_func.md`
- `docs\studies-controller-endpoints.md`
