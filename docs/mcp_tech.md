# MCP Technical Documentation

## Purpose

This document describes the technical design and implementation of the HTTP MCP server in this repository.

The MCP server exposes the existing ClinicalTrials.gov study lookup capability through MCP while preserving alignment with the REST API already exposed by `ClinicalTrials.Api`.

## Solution Overview

The implementation is split into three application layers:

1. `ClinicalTrials.Api`
   - existing ASP.NET Core REST API
   - exposes `GET /api/studies/{nctId}`
2. `ClinicalTrials.Mcp`
   - ASP.NET Core HTTP MCP host
   - exposes `/mcp`, `/health/live`, and `/health/ready`
   - registers MCP tools through the official C# MCP SDK
   - calls a configurable study service over HTTP
3. `ClinicalTrials.Shared`
   - shared lookup abstraction and implementation
   - encapsulates the ClinicalTrials.gov upstream call
   - used by `ClinicalTrials.Api`

This separation keeps transport concerns isolated while ensuring the study lookup operation is implemented once.

## Technology Stack

- .NET `net10.0`
- ASP.NET Core
- `ModelContextProtocol.AspNetCore` `1.1.0`
- `Microsoft.Extensions.Http`
- ASP.NET Core authentication and authorization
- xUnit for automated tests

## Host Architecture

### MCP Host Responsibilities

- host MCP over stateless streamable HTTP
- discover and register MCP tools from assembly
- protect `/mcp` with API key authentication
- expose anonymous liveness and readiness endpoints
- call a configurable external study service
- preserve a local, non-Azure development path

### Key Startup Behavior

`ClinicalTrials.Mcp/ClinicalTrials.Mcp/Program.cs` now:

- validates API key configuration on startup
- registers the `ApiKey` authentication scheme
- registers an authorization policy that requires `clinicaltrials.client_active = True`
- conditionally enables forwarded headers when `Hosting:ForwardedHeaders:Enabled` is true
- registers a readiness health check
- configures MCP using:
  - `AddMcpServer()`
  - `WithHttpTransport(options => options.Stateless = true)`
  - `WithToolsFromAssembly(...)`
- maps:
  - `GET /health/live`
  - `GET /health/ready`
  - `POST/GET /mcp` via `MapMcp("/mcp").RequireAuthorization(...)`

The old permissive CORS policy has been removed from the default host pipeline.

## Lookup Routing

The MCP host no longer calls ClinicalTrials.gov directly.

It now depends on `IStudyLookupEndpointClient`, implemented by `StudyServiceHttpClient`, which calls a configurable upstream study service.

Current local default:

- base URL: `http://localhost:5010/`
- path template: `api/studies/{nctId}`

That means the runtime flow is:

1. MCP client calls `ClinicalTrials.Mcp`
2. `ClinicalTrials.Mcp` calls `ClinicalTrials.Api`
3. `ClinicalTrials.Api` calls ClinicalTrials.gov

This keeps the MCP host independent from the direct ClinicalTrials.gov integration and reduces future disruption if the REST service is replaced by another compatible study service.

## Authentication Design

### Scheme

- scheme name: `ApiKey`
- request header: `X-Api-Key`

### Current Key Source

Phase 1 uses configuration-backed client keys.

Local development is seeded through `appsettings.Development.json`:

- client ID: `local-dev-client`
- API key: `clinical-trials-local-dev-key`
- study service base URL: `http://localhost:5010/`

Non-local environments should override keys through environment variables, user-secrets, or a managed secret store.

### Auth Behavior

- missing API key header -> `401 Unauthorized`
- invalid API key -> `401 Unauthorized`
- configured but disabled client -> authenticated, then rejected by policy with `403 Forbidden`
- active configured client -> authorized to access `/mcp`

### Claims

Successful authentication emits:

- `ClaimTypes.NameIdentifier`
- `ClaimTypes.Name`
- `clinicaltrials.client_id`
- `clinicaltrials.client_active`

## Health Endpoints

### `/health/live`

- confirms the process is running
- does not test upstream reachability

### `/health/ready`

- confirms required startup configuration is present for the current phase
- currently validates MCP host configuration readiness, not REST API reachability or future SQL/Redis connectivity

## REST API Shared Service Design

### Contract

The shared contract is:

- `IStudyLookupService`

Method:

```csharp
Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
```

### Result Model

`StudyLookupResult` carries enough information for both transports to interpret the lookup outcome:

- `StatusCode`
- `ContentType`
- `Body`
- `FailureKind`
- `Exception`

### Failure Classification

`StudyLookupFailureKind` currently supports:

- `None`
- `RequestFailed`
- `TimedOut`

## MCP Study Service Client Design

### Contract

The MCP host uses:

- `IStudyLookupEndpointClient`

Method:

```csharp
Task<StudyLookupResult> GetStudyAsync(string nctId, CancellationToken cancellationToken)
```

### Current HTTP Implementation

`StudyServiceHttpClient`:

- uses named `HttpClient` `StudyService`
- reads `StudyService:BaseUrl`
- reads `StudyService:StudyLookupPathTemplate`
- replaces `{nctId}` in the configured path template
- forwards response status, content type, and body
- maps `HttpRequestException` to `RequestFailed`
- maps non-caller-cancel `TaskCanceledException` to `TimedOut`

## Upstream Request Behavior

The REST API shared service:

- creates the named `HttpClient` `ClinicalTrialsGov`
- builds the path `api/v2/studies/{Uri.EscapeDataString(nctId)}`
- performs a `GET`
- reads the upstream body as string
- preserves upstream status code and content type
- maps `HttpRequestException` to `RequestFailed`
- maps non-caller-cancel `TaskCanceledException` to `TimedOut`

## Configuration

### Required Active Configuration

The MCP host requires:

```json
{
  "StudyService": {
    "BaseUrl": "http://localhost:5010/",
    "StudyLookupPathTemplate": "api/studies/{nctId}"
  },
  "Authentication": {
    "ApiKeys": {
      "HeaderName": "X-Api-Key",
      "Clients": [
        {
          "ClientId": "local-dev-client",
          "ApiKey": "clinical-trials-local-dev-key",
          "Enabled": true
        }
      ]
    }
  }
}
```

Startup fails if required auth or study-service configuration is missing.

### Production-Oriented Host Defaults

- `AllowedHosts` defaults to local addresses only
- forwarded headers are opt-in through `Hosting:ForwardedHeaders:Enabled`
- no permissive CORS policy is applied by default

### Future Configuration Contracts

The MCP appsettings file now includes placeholder configuration sections for later phases:

- `ConnectionStrings:ClientRegistry`
- `ConnectionStrings:Redis`
- `Secrets:Provider`
- `Telemetry:ApplicationInsights:ConnectionString`
- `RateLimiting:*`

These are scaffolding for later production phases and are not active yet.

## Transport Mode

The MCP host is configured for stateless streamable HTTP mode.

Implications:

- the server does not issue or depend on `MCP-Session-Id`
- requests are independent
- server restarts do not create stale session problems for this tool
- server-to-client features that depend on stateful sessions are unavailable

## MCP Tool Design

### Registered Tool

- `get_study_by_nct_id`

Declared characteristics:

- `ReadOnly = true`
- `Idempotent = true`
- `Destructive = false`
- `OpenWorld = true`
- `UseStructuredContent = true`

### Input

- `nctId: string`

Validation:

- trims whitespace
- rejects null, empty, and whitespace-only values
- does not enforce stricter NCT formatting in v1

### Success Result

On upstream `2xx`:

- `IsError = false`
- `Content` contains a human-readable summary
- `StructuredContent` contains the raw upstream JSON payload

### Error Result

On failure:

- `IsError = true`
- `Content` contains readable error text
- `StructuredContent` contains structured error metadata

Current mappings:

- invalid input -> `400`
- upstream connectivity failure -> `502`
- upstream timeout -> `504`
- upstream non-2xx -> original upstream status code
- unexpected missing status code -> `500`

## Request Flow

### Successful Tool Call

1. client sends `X-Api-Key` and calls `/mcp`
2. authentication and authorization succeed
3. MCP runtime dispatches `get_study_by_nct_id`
4. tool trims and validates `nctId`
5. tool calls `IStudyLookupEndpointClient.GetStudyAsync`
6. MCP HTTP client calls the configured study service endpoint
7. the study service returns a response from its own backing system
8. tool converts the response to summary content plus structured JSON
9. MCP host returns `CallToolResult`

### Failed Auth Call

1. client calls `/mcp` without a valid `X-Api-Key`
2. authentication fails or authorization rejects the client
3. tool execution does not start
4. ASP.NET Core returns `401` or `403`

## Testing Strategy

Automated coverage includes:

- shared service pass-through and failure mapping
- REST API integration behavior
- MCP tool success and failure behavior
- MCP study-service client path construction and failure mapping
- MCP host liveness and readiness endpoints
- unauthenticated `/mcp` rejection
- disabled client `/mcp` rejection
- authenticated MCP tool discovery and invocation

The current test suite passes with 19 tests.

## Workspace Client Configuration

A sample workspace configuration is provided at `.vscode/mcp.json`:

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

## Current Technical Limitations

- only one MCP tool is implemented
- phase 1 still uses configuration-backed API keys rather than a database-backed registry
- MCP depends on a separately running study service
- no rate limiting yet
- no structured request correlation yet
- no SQL or Redis dependency checks yet
- no deployment packaging or infrastructure automation yet

## Recommended Next Technical Steps

1. Replace configuration-backed API keys with a SQL-backed registry and hashed secrets.
2. Add rate limiting and cache infrastructure.
3. Add structured request correlation and audit logging.
4. Extend readiness checks for future SQL and Redis dependencies.
5. Add deployment packaging and infrastructure automation.

## Source References

- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Program.cs`
- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Authentication`
- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Health`
- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Tools/StudiesMcpTools.cs`
- `ClinicalTrials.Shared/ClinicalTrials.Shared`
- `ClinicalTrials.Tests/ClinicalTrials.Tests`
