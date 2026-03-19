# How To Run

This document explains how to run the ClinicalTrials REST API, the MCP server, and the automated tests from this repository.

## Prerequisites

Make sure the following are available on your machine:

- .NET SDK `10.x`
- internet access to reach `https://clinicaltrials.gov/`
- PowerShell or a terminal that can run `dotnet`

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

To build the full solution:

```powershell
dotnet build .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

## Run The REST API

The REST API project is:

```text
ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Run it with:

```powershell
dotnet run --project .\ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.csproj
```

Default development URLs:

- `http://localhost:5010`
- `https://localhost:7001`

### Verify The REST API

Open the study endpoint in a browser or run:

```powershell
Invoke-WebRequest http://localhost:5010/api/studies/NCT04924608
```

Expected result:

- HTTP `200 OK`
- a JSON study payload returned from ClinicalTrials.gov

You can also use the repo HTTP file:

```text
ClinicalTrials.Api\ClinicalTrials.Api\ClinicalTrials.Api.http
```

## Run The MCP Server

The MCP server project is:

```text
ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

Run it with:

```powershell
dotnet run --project .\ClinicalTrials.Mcp\ClinicalTrials.Mcp\ClinicalTrials.Mcp.csproj
```

Default development URLs:

- `http://localhost:5011`
- `https://localhost:7002`

Important endpoints:

- MCP endpoint: `http://localhost:5011/mcp`
- health endpoint: `http://localhost:5011/health`

### Verify The MCP Server

Check the health endpoint:

```powershell
Invoke-WebRequest http://localhost:5011/health
```

Expected result:

- HTTP `200 OK`
- response body `healthy`

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
      "url": "http://localhost:5011/mcp"
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

- the tool returns the raw study JSON as structured content

## Run Automated Tests

To run all tests:

```powershell
dotnet test .\ClinicalTrials.Api\ClinicalTrials.Api.slnx
```

The test suite covers:

- shared lookup service behavior
- REST API integration behavior
- MCP tool behavior
- MCP host integration and tool discovery

## Run API And MCP Together

If you want both hosts available at the same time, open two terminals from the repo root.

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
- verify `ClinicalTrials:BaseUrl` is present in app settings

### Port Already In Use

Symptoms:

- `dotnet run` fails to start

Checks:

- free ports `5010`, `5011`, `7001`, or `7002`
- stop previous runs of the API or MCP host

### MCP Client Cannot Connect

Checks:

- verify the MCP server is running
- verify `.vscode\mcp.json` points to `http://localhost:5011/mcp`
- verify `http://localhost:5011/health` responds successfully

## Related Documents

- `docs\mcp-server.md`
- `docs\mcp_tech.md`
- `docs\mcp_func.md`
- `docs\studies-controller-endpoints.md`
