# ClinicalTrials MCP Server

This document describes the Phase 3 HTTP MCP server that exposes study lookup for ClinicalTrials.gov through `ClinicalTrials.Api`.

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
- Local client registry backing store: SQL Server
- Local cache and rate limiting: in-memory
- MCP request timeout: `30` seconds by default
- MCP max request body size: `1048576` bytes by default

The MCP server does not call ClinicalTrials.gov directly. It calls a configurable study service, and the local default points to `ClinicalTrials.Api`.

## Local Prerequisites

Before local MCP calls will work:

1. run [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql)
2. run [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql)
3. set `ConnectionStrings__ClientRegistry`
4. start `ClinicalTrials.Api`
5. start `ClinicalTrials.Mcp`

## Tool Contract

### `get_study_by_nct_id`

Fetches a study by NCT identifier through the configured study service.

Input:

- `nctId` (`string`): NCT ID such as `NCT04924608`

Success behavior:

- trims leading and trailing whitespace
- calls the configured study service
- returns a clean summary in visible tool content
- returns the upstream study payload as structured JSON
- caches successful responses
- caches `404` responses for a short negative-cache TTL

Failure behavior:

- invalid input returns a structured tool error with status `400`
- study-service `404` and other non-2xx responses return structured tool errors with upstream status metadata
- study-service connectivity failures return status `502`
- study-service timeout returns status `504`
- MCP endpoint rate-limit exhaustion returns HTTP `429`
- oversized MCP requests return HTTP `413`

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

1. Start `ClinicalTrials.Api`.
2. Start `ClinicalTrials.Mcp`.
3. Verify liveness and readiness:

```powershell
Invoke-WebRequest http://localhost:5011/health/live
Invoke-WebRequest http://localhost:5011/health/ready
```

4. In an MCP-capable client, invoke:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected result:

- the client sends `X-Api-Key: clinical-trials-local-dev-key`
- the tool returns a readable summary in visible content
- the tool returns the raw study JSON as structured content
- the request path used by MCP defaults to `http://localhost:5010/api/studies/{nctId}`
- `/health/ready` confirms SQL reachability and, when enabled, Redis reachability
