# MCP Technical Documentation

## Purpose

This document describes the technical design and implementation of the HTTP MCP server in this repository.

The MCP server exposes the existing ClinicalTrials.gov study lookup capability through the Model Context Protocol (MCP), while preserving alignment with the REST API already exposed by `ClinicalTrials.Api`.

## Solution Overview

The implementation is split into three application layers:

1. `ClinicalTrials.Api`
   - Existing ASP.NET Core REST API
   - Exposes `GET /api/studies/{nctId}`

2. `ClinicalTrials.Mcp`
   - New ASP.NET Core HTTP MCP host
   - Exposes `/mcp` and `/health`
   - Registers MCP tools through the official C# MCP SDK

3. `ClinicalTrials.Shared`
   - Shared lookup abstraction and implementation
   - Encapsulates the ClinicalTrials.gov upstream call
   - Used by both the REST API and the MCP host

This separation keeps transport concerns isolated while ensuring the business operation for study lookup is implemented once.

## Project Structure

- `ClinicalTrials.Api/ClinicalTrials.Api`
  - REST host
- `ClinicalTrials.Mcp/ClinicalTrials.Mcp`
  - MCP host
- `ClinicalTrials.Shared/ClinicalTrials.Shared`
  - shared service and service registration
- `ClinicalTrials.Tests/ClinicalTrials.Tests`
  - unit and integration tests
- `docs`
  - implementation and behavior documentation

## Technology Stack

- .NET `net10.0`
- ASP.NET Core
- `ModelContextProtocol.AspNetCore` `1.1.0`
- `Microsoft.Extensions.Http`
- xUnit for test coverage

## Host Architecture

### MCP Host

The MCP host is an ASP.NET Core application configured in `ClinicalTrials.Mcp`.

Responsibilities:

- host MCP over HTTP using streamable HTTP transport
- run MCP in stateless HTTP mode
- discover and register tool classes from assembly
- expose a non-MCP `/health` endpoint
- apply permissive CORS for local development
- reuse the shared ClinicalTrials.gov lookup service

Key startup behavior:

- registers CORS with `AllowAnyOrigin`, `AllowAnyHeader`, `AllowAnyMethod`
- registers shared ClinicalTrials.gov lookup services
- configures MCP using:
  - `AddMcpServer()`
  - `WithHttpTransport(options => options.Stateless = true)`
  - `WithToolsFromAssembly(...)`
- maps:
  - `GET /health`
  - `POST/GET /mcp` via `MapMcp("/mcp")`

### REST Host

The existing REST API remains in `ClinicalTrials.Api`.

The controller now delegates the upstream study retrieval to `IStudyLookupService`. This preserves the existing external REST contract while removing duplicate upstream call logic.

## Shared Service Design

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

This allows the REST API and MCP tool to map the same upstream failure into transport-appropriate responses without duplicating exception handling.

### Upstream Request Behavior

The shared service:

- creates the named `HttpClient` `ClinicalTrialsGov`
- builds the path `api/v2/studies/{Uri.EscapeDataString(nctId)}`
- performs a `GET`
- reads the upstream body as string
- preserves upstream status code and content type
- maps `HttpRequestException` to `RequestFailed`
- maps non-caller-cancel `TaskCanceledException` to `TimedOut`

## Dependency Injection

Shared registration is centralized in:

- `ClinicalTrialsServiceCollectionExtensions`

Registered services:

- named `HttpClient`: `ClinicalTrialsGov`
- scoped `IStudyLookupService`

This extension is used by both the REST host and the MCP host.

## Configuration

### Required Configuration

Both the REST API and MCP host depend on:

```json
{
  "ClinicalTrials": {
    "BaseUrl": "https://clinicaltrials.gov/"
  }
}
```

Configuration key:

- `ClinicalTrials:BaseUrl`

If the key is missing, application startup fails with an `InvalidOperationException`.

### Development URLs

Current MCP launch settings expose:

- `http://localhost:5011`
- `https://localhost:7002`

Current local MCP endpoint:

- `http://localhost:5011/mcp`

Current health endpoint:

- `http://localhost:5011/health`

### Transport Mode

The MCP host is configured for stateless Streamable HTTP mode.

Implications:

- the server does not use `MCP-Session-Id`
- requests are independent from one another
- restarting the MCP host does not invalidate a client-held session ID because no session ID is issued
- legacy SSE session behavior is not used
- server-to-client requests such as elicitation and sampling are unavailable

## MCP Tool Design

### Registered Tool

Tool name:

- `get_study_by_nct_id`

Declared characteristics:

- `ReadOnly = true`
- `Idempotent = true`
- `Destructive = false`
- `OpenWorld = true`
- `UseStructuredContent = true`

### Tool Input

Input parameter:

- `nctId: string`

Validation:

- trims whitespace
- rejects null/empty/whitespace values
- does not enforce regex or stricter NCT formatting in v1

### Success Response

On upstream `2xx`:

- the tool parses the upstream JSON
- returns a `CallToolResult`
- sets `IsError = false`
- provides:
  - `Content`: clean human-readable summary derived from the study payload
  - `StructuredContent`: raw upstream JSON payload as `JsonElement`

### Error Response

On failure, the tool returns `CallToolResult` with:

- `IsError = true`
- `Content`: human-readable error text
- `StructuredContent`: structured error metadata

Structured error payload includes:

- `statusCode`
- `title`
- `detail`
- optional `upstreamBody`

Current mappings:

- invalid input -> `400`
- upstream connectivity failure -> `502`
- upstream timeout -> `504`
- upstream non-2xx -> original upstream status code
- unexpected missing status code from shared service -> `500`

### Error Semantics

The tool uses MCP tool-level errors, not protocol-level JSON-RPC errors, for business and upstream failures. This is deliberate because MCP clients and models can inspect the returned error content and potentially self-correct.

## Request Flow

### Successful Tool Call

1. MCP client calls `/mcp`
2. MCP runtime dispatches `get_study_by_nct_id`
3. tool trims and validates `nctId`
4. tool calls `IStudyLookupService.GetStudyAsync`
5. shared service calls `https://clinicaltrials.gov/api/v2/studies/{nctId}`
6. upstream response is returned to shared service
7. tool converts the raw JSON body into `StructuredContent`
8. MCP host returns `CallToolResult`

### Upstream Failure Flow

1. tool calls shared service
2. shared service classifies the failure
3. tool maps the failure into structured MCP error output
4. client receives an error tool result with status metadata

## REST API Alignment

The REST controller and MCP tool share the same upstream call implementation.

This guarantees alignment in:

- upstream URL path
- URL escaping behavior
- timeout/network failure classification
- response pass-through semantics

The transport-specific behavior differs only at the final mapping layer:

- REST maps to HTTP responses and `ProblemDetails`
- MCP maps to `CallToolResult`

## Testing Strategy

The solution includes automated coverage across shared logic, REST behavior, and MCP behavior.

### Shared Service Tests

Coverage includes:

- success pass-through
- upstream non-2xx pass-through
- network failure mapping
- timeout mapping

### REST Integration Tests

Coverage includes:

- `200` success
- upstream `404`
- invalid route value behavior
- `502` for connectivity failure
- `504` for timeout

### MCP Tool Tests

Coverage includes:

- successful structured JSON result
- whitespace trimming
- invalid input error
- upstream `404` error
- upstream connectivity error
- upstream timeout error

### MCP Host Integration Tests

Coverage includes:

- `/health` endpoint
- MCP tool discovery
- MCP tool invocation through an MCP client

## Operational Notes

### Stateless MCP

The current MCP host uses stateless HTTP mode because the server only exposes a simple read-only lookup tool.

Benefits for this implementation:

- avoids stale session errors after server restarts
- simplifies local development
- is a better fit for independent request/response tools
- improves load-balancing compatibility if the host is later deployed behind multiple instances

### CORS

The MCP host currently uses permissive CORS intended for local development:

- any origin
- any header
- any method

This should be restricted before any shared or public deployment.

### Authentication

There is no authentication or authorization on the MCP host in the current implementation.

This is acceptable for local development only.

### Logging

The MCP tool logs:

- upstream connectivity failures as errors
- upstream timeouts as warnings

Structured request correlation and audit logging are not yet implemented.

## Workspace Client Configuration

A sample workspace MCP client configuration is provided at:

- `.vscode/mcp.json`

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

## Limitations

- only one MCP tool is implemented
- tool assumes upstream success payload is valid JSON
- no schema projection or summary view is provided
- no rate limiting
- no authentication
- no deployment packaging or infrastructure automation
- permissive CORS is not production-safe

## Recommended Next Technical Steps

1. Add authentication and authorization for non-local use.
2. Replace permissive CORS with an allowlist.
3. Add structured invocation logging and correlation IDs.
4. Add rate limiting and production exception handling.
5. Consider a second shared abstraction if more study operations are added.

## Source References

- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Program.cs`
- `ClinicalTrials.Mcp/ClinicalTrials.Mcp/Tools/StudiesMcpTools.cs`
- `ClinicalTrials.Shared/ClinicalTrials.Shared/ClinicalTrialsServiceCollectionExtensions.cs`
- `ClinicalTrials.Shared/ClinicalTrials.Shared/StudyLookupService.cs`
- `ClinicalTrials.Api/ClinicalTrials.Api/Controllers/StudiesController.cs`
- `ClinicalTrials.Tests/ClinicalTrials.Tests`
