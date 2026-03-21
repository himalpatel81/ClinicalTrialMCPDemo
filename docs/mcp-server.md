# ClinicalTrials MCP Server

This document describes how to run and test the HTTP MCP server that exposes study lookup for ClinicalTrials.gov.

## Overview

- Project: `ClinicalTrials.Mcp`
- MCP transport: HTTP
- MCP mode: stateless streamable HTTP
- MCP endpoint: `http://localhost:5011/mcp`
- liveness endpoint: `http://localhost:5011/health/live`
- readiness endpoint: `http://localhost:5011/health/ready`
- Tool: `get_study_by_nct_id`
- Local development auth header: `X-Api-Key: clinical-trials-local-dev-key`
- Local development study service target: `http://localhost:5010/api/studies/{nctId}`

The MCP server no longer calls ClinicalTrials.gov directly.
It calls a configurable external study service, and the local default points to `ClinicalTrials.Api`.
It is configured in stateless mode, which avoids stale `MCP-Session-Id` problems for this read-only tool scenario.
Local execution remains a required use case, and the local path remains independent of Azure-managed services.

## Run The MCP Server

From the repository root:

Terminal 1:

```powershell
dotnet run --project .\ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Terminal 2:

```powershell
dotnet run --project .\ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

The REST API must be running first for local MCP lookups to succeed.

## Tool Contract

### `get_study_by_nct_id`

Fetches a ClinicalTrials.gov study by NCT identifier.

Input:

- `nctId` (`string`): ClinicalTrials.gov NCT ID such as `NCT04924608`

Success behavior:

- trims leading and trailing whitespace
- calls the shared ClinicalTrials.gov proxy service
- returns a clean summary in the visible tool content
- returns the upstream study payload as structured JSON

Failure behavior:

- invalid input returns a structured tool error with status `400`
- study-service `404` and other non-2xx responses return structured tool errors with upstream status metadata
- study-service connectivity failures return status `502`
- study-service timeout returns status `504`

## Connect A Workspace MCP Client

The repository includes a sample workspace configuration at `.vscode/mcp.json`:

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

## Quick Smoke Test

1. Start the MCP server.
2. Verify liveness and readiness:

```powershell
Invoke-WebRequest http://localhost:5011/health/live
Invoke-WebRequest http://localhost:5011/health/ready
```

3. In an MCP-capable client, invoke:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected result:

- the client sends `X-Api-Key: clinical-trials-local-dev-key`
- the tool returns a readable summary in visible content
- the tool returns the raw study JSON as structured content
- errors are returned as MCP tool errors with status metadata
- the request path used by MCP defaults to `http://localhost:5010/api/studies/{nctId}`
