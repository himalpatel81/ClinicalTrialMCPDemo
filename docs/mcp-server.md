# ClinicalTrials MCP Server

This document describes how to run and test the HTTP MCP server that exposes study lookup for ClinicalTrials.gov.

## Overview

- Project: `ClinicalTrials.Mcp`
- MCP transport: HTTP
- MCP endpoint: `http://localhost:5011/mcp`
- Health endpoint: `http://localhost:5011/health`
- Tool: `get_study_by_nct_id`

The MCP server shares the same ClinicalTrials.gov lookup service as the REST API so both surfaces stay aligned.

## Run The MCP Server

From the repository root:

```powershell
dotnet run --project .\ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

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
- upstream `404` and other non-2xx responses return structured tool errors with upstream status metadata
- upstream connectivity failures return status `502`
- upstream timeout returns status `504`

## Connect A Workspace MCP Client

The repository includes a sample workspace configuration at `.vscode/mcp.json`:

```json
{
  "servers": {
    "clinical-trials-mcp": {
      "type": "http",
      "url": "http://localhost:5011/mcp"
    }
  },
  "inputs": []
}
```

## Quick Smoke Test

1. Start the MCP server.
2. Verify the health endpoint:

```powershell
Invoke-WebRequest http://localhost:5011/health
```

3. In an MCP-capable client, invoke:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected result:

- the tool returns the raw study JSON as structured content
- errors are returned as MCP tool errors with status metadata
