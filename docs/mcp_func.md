# MCP Functional Documentation

## Purpose

This document describes the functional behavior of the MCP server from a consumer perspective.

The MCP server allows an MCP-capable client to retrieve a ClinicalTrials.gov study by NCT ID using a single tool call.

## Functional Summary

The server provides:

- one protected MCP tool endpoint
- one MCP tool for study lookup
- two HTTP health endpoints for runtime verification

Current scope:

- retrieve one study by NCT ID
- return a readable summary plus raw ClinicalTrials.gov study payload on success
- return structured error information on failure
- route the lookup through the configured study service rather than calling ClinicalTrials.gov directly

Out of scope:

- search across studies
- list studies
- create or modify data
- reshape the payload into a custom business DTO

## Supported User Capability

A user can ask an MCP-capable assistant or client to fetch a study by its ClinicalTrials.gov NCT identifier.

Example intents:

- `Get study NCT04924608`
- `Look up trial NCT04924608`
- `Fetch the ClinicalTrials.gov record for NCT04924608`

## Available MCP Tool

### `get_study_by_nct_id`

Functional purpose:

- retrieve the study record for a single NCT ID

Input:

- `nctId` as text

Example:

```text
NCT04924608
```

## Functional Inputs

### Accepted Input

- non-empty string
- leading and trailing spaces are removed before processing

Examples accepted:

- `NCT04924608`
- ` NCT04924608 `

### Rejected Input

- empty string
- whitespace-only string

Examples rejected:

- `""`
- `"   "`

## Functional Outputs

### Success Output

When ClinicalTrials.gov returns success:

- `IsError` is `false`
- the visible tool output contains a readable summary
- the raw study payload is returned as structured JSON

Functional expectation:

- the client can show useful text in the UI
- the client can still inspect the full raw study data

### Failure Output

When the request cannot be completed:

- `IsError` is `true`
- the visible tool output contains a readable error message
- structured error metadata is returned

Error metadata contains:

- `statusCode`
- `title`
- `detail`
- optional `upstreamBody`

## Functional Access Rules

### Rule 1: Authenticated MCP Access

The MCP endpoint requires an API key.

Functional meaning:

- clients must send `X-Api-Key`
- local development uses `clinical-trials-local-dev-key`
- missing or invalid keys are rejected before tool execution
- disabled clients are rejected even if the key value matches configuration

### Rule 2: Indirect Upstream Access

The MCP server does not call ClinicalTrials.gov directly.

Functional meaning:

- MCP calls the configured study service
- local development defaults that study service to `ClinicalTrials.Api`
- future replacement of `ClinicalTrials.Api` should require only configuration or client-implementation changes

### Rule 3: Read-Only Behavior

The tool is read-only.

Functional meaning:

- it does not modify local data
- it does not modify ClinicalTrials.gov data
- repeated calls are safe from a mutation perspective

### Rule 4: Raw Payload Preservation

The tool preserves the upstream study payload in structured content.

Functional meaning:

- downstream consumers can use the source JSON directly
- the server does not impose a custom DTO

### Rule 5: Structured Error Reporting

Failures are returned as structured tool errors instead of plain text only.

Functional meaning:

- clients can branch on `statusCode`
- users can see readable failure information
- models can potentially self-correct

## Functional Scenarios

### Scenario 1: Valid NCT ID

Input:

- `nctId = NCT04924608`

Expected outcome:

- the authenticated client receives summary content and raw study JSON
- the lookup is sent through the configured study service

### Scenario 2: Input Contains Extra Spaces

Input:

- `nctId = " NCT04924608 "`

Expected outcome:

- spaces are trimmed
- the same study result is returned

### Scenario 3: Empty Input

Input:

- `nctId = ""`

Expected outcome:

- tool returns `400`
- title is `Invalid NCT ID`

### Scenario 4: Missing API Key

Expected outcome:

- request is rejected before tool execution
- HTTP status is `401`

### Scenario 5: Disabled Client

Expected outcome:

- request is rejected before tool execution
- HTTP status is `403`

### Scenario 6: Study Not Found

Expected outcome:

- tool returns structured `404`

### Scenario 7: Upstream Service Unreachable

Expected outcome:

- tool returns structured `502`

### Scenario 8: Upstream Timeout

Expected outcome:

- tool returns structured `504`

## Health Endpoints

### `GET /health/live`

Functional purpose:

- confirm that the MCP host process is running

### `GET /health/ready`

Functional purpose:

- confirm that required startup configuration is present for the current phase

What the health endpoints do not confirm:

- that ClinicalTrials.gov is reachable
- that future SQL or Redis dependencies are available
- that the configured study service is currently reachable

## Functional Relationship To REST API

The MCP server and REST API expose the same study lookup capability through different interfaces.

REST consumer:

- calls `GET /api/studies/{nctId}`

MCP consumer:

- calls `get_study_by_nct_id`

Shared functional behavior:

- same study data ultimately returned by the REST API path
- same study path lookup
- same timeout and connectivity classification

Different presentation:

- REST returns HTTP responses
- MCP returns tool results

## Consumer Expectations

Clients should expect:

- one tool only in v1
- API key authentication on `/mcp`
- the configured study service to be available
- raw JSON study output on success
- readable summary content on success
- structured status-driven error output on failure
- local development endpoint at `http://localhost:5011/mcp`
- stateless tool calls with no long-lived client session requirement

Clients should not assume:

- search or list capabilities
- stable summary wording beyond the current implementation
- production internet availability

## Example Local Authenticated Interaction

Required local header:

```text
X-Api-Key: clinical-trials-local-dev-key
```

Example prompt:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected behavior:

1. the client sends the local API key
2. the assistant selects the MCP tool
3. the tool is called with the supplied NCT ID
4. MCP calls `ClinicalTrials.Api`
5. the study is retrieved
6. the assistant receives summary content plus structured study JSON

## Acceptance Criteria

The implementation is functionally correct when all of the following are true:

- the MCP host starts successfully
- `/health/live` returns `200 OK`
- `/health/ready` returns `200 OK`
- `/mcp` rejects missing or invalid API keys
- `get_study_by_nct_id` is visible to an authenticated MCP client
- valid NCT IDs return structured study JSON
- whitespace-only input returns `400`
- upstream not found returns structured `404`
- upstream connectivity failure returns `502`
- upstream timeout returns `504`

## Current Constraints

- local/dev-first design
- no search or list tool
- no database-backed client registry yet
- no rate limiting yet
- no production infrastructure automation yet
- local MCP lookup depends on a running local study service

## Recommended Next Functional Steps

1. Add a database-backed client registry and key lifecycle flow.
2. Add more study-related tools only when there is a clear consumer need.
3. Decide whether users need a curated summary tool in addition to raw JSON.
