# MCP Functional Documentation

## Purpose

This document describes the functional behavior of the MCP server from a consumer perspective.

The MCP server allows an MCP-capable client to retrieve a ClinicalTrials.gov study by NCT ID using a single tool call.

## Functional Summary

The server provides:

- one MCP tool for study lookup
- one HTTP health endpoint for runtime verification

Current scope:

- retrieve one study by NCT ID
- return the raw ClinicalTrials.gov study payload on success
- return structured error information on failure

Out of scope:

- search across studies
- list studies
- create or modify data
- summarize or transform the payload into a custom business shape

## Supported User Capability

### Capability

A user can ask an MCP-capable assistant or client to fetch a study by its ClinicalTrials.gov NCT identifier.

Example user intent:

- "Get study NCT04924608"
- "Look up trial NCT04924608"
- "Fetch the ClinicalTrials.gov record for NCT04924608"

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

Expected usage:

- the client invokes the tool with an `nctId`
- the tool contacts ClinicalTrials.gov through the shared service
- the result is returned to the client

## Functional Inputs

### Accepted Input

The tool accepts:

- a non-empty string
- leading/trailing spaces are allowed and removed before processing

Examples accepted:

- `NCT04924608`
- ` NCT04924608 `

### Rejected Input

The tool rejects:

- empty string
- whitespace-only string

Examples rejected:

- `""`
- `"   "`

## Functional Outputs

### Success Output

When the study exists and ClinicalTrials.gov responds successfully:

- the tool returns a success result
- `IsError` is `false`
- a clean human-readable summary is included
- the full study payload is returned as structured JSON

Functional expectation:

- the client can show a readable summary in the UI
- the client can inspect the raw study data directly
- no important study fields are removed by this server

### Failure Output

When the request cannot be completed:

- the tool returns an error result
- `IsError` is `true`
- a readable error message is included
- structured error metadata is included

Error metadata contains:

- `statusCode`
- `title`
- `detail`
- optional `upstreamBody`

## Functional Scenarios

### Scenario 1: Valid NCT ID

Input:

- `nctId = NCT04924608`

Behavior:

- tool validates the input
- tool calls ClinicalTrials.gov
- upstream returns study JSON
- tool returns success

Expected outcome:

- user receives the study payload as structured JSON

### Scenario 2: Input Contains Extra Spaces

Input:

- `nctId = " NCT04924608 "`

Behavior:

- tool trims spaces
- continues with the cleaned value

Expected outcome:

- same result as using `NCT04924608`

### Scenario 3: Empty Input

Input:

- `nctId = ""`

Behavior:

- tool stops before calling upstream
- returns validation error

Expected outcome:

- error status code `400`
- title `Invalid NCT ID`

### Scenario 4: Study Not Found

Input:

- valid-looking NCT ID that ClinicalTrials.gov does not return

Behavior:

- tool calls ClinicalTrials.gov
- upstream returns `404`
- tool returns structured tool error

Expected outcome:

- error status code `404`
- title `ClinicalTrials.gov returned an error`
- detail explains the upstream status

### Scenario 5: Upstream Service Unreachable

Behavior:

- network call to ClinicalTrials.gov fails

Expected outcome:

- error status code `502`
- title `ClinicalTrials.gov request failed`

### Scenario 6: Upstream Timeout

Behavior:

- ClinicalTrials.gov does not respond in time

Expected outcome:

- error status code `504`
- title `ClinicalTrials.gov request timed out`

## Functional Rules

### Rule 1: Read-Only Behavior

The tool is read-only.

Functional meaning:

- it does not modify any local data
- it does not modify any ClinicalTrials.gov data
- repeated calls are safe from a data mutation perspective

### Rule 2: Raw Payload Preservation

The tool returns the upstream study payload without business reshaping.

Functional meaning:

- downstream consumers see the source study structure
- the server does not produce a custom study DTO

### Rule 3: Structured Error Reporting

Failures are returned as structured tool errors instead of unstructured plain text.

Functional meaning:

- the user or client can understand what failed
- automated clients can branch on `statusCode`
- models can potentially self-correct with a new call

## Health Endpoint

### `GET /health`

Functional purpose:

- confirm that the MCP host process is running and responding

What it does:

- returns HTTP `200 OK`
- returns `healthy`

What it does not confirm:

- that ClinicalTrials.gov is reachable
- that the study lookup tool will succeed for a given NCT ID

## Functional Relationship To REST API

The MCP server and REST API expose the same study lookup capability through different interfaces.

REST consumer:

- calls `GET /api/studies/{nctId}`

MCP consumer:

- calls `get_study_by_nct_id`

Shared functional behavior:

- same upstream service
- same study path lookup
- same timeout/connectivity classification

Different presentation:

- REST returns HTTP responses
- MCP returns tool results

## Consumer Expectations

Clients should expect:

- one tool only in v1
- raw JSON study output on success
- structured status-driven error output on failure
- local development HTTP endpoint at `http://localhost:5011/mcp`
- stateless tool calls with no long-lived client session requirement

Clients should not assume:

- stable summary field names beyond the upstream payload
- public internet availability
- authentication support in the current version

## Example Functional Interaction

Example prompt in an MCP-capable assistant:

```text
Use get_study_by_nct_id with nctId NCT04924608
```

Expected behavior:

1. the assistant selects the MCP tool
2. the tool is called with the supplied NCT ID
3. the study is retrieved
4. the assistant receives structured study JSON

## Acceptance Criteria

The implementation is functionally correct when all of the following are true:

- the MCP host starts successfully
- `/health` returns `200 OK`
- `get_study_by_nct_id` is visible to an MCP client
- valid NCT IDs return structured study JSON
- whitespace-only input returns `400`
- upstream not found returns structured `404`
- upstream connectivity failure returns `502`
- upstream timeout returns `504`

## Current Constraints

- local/dev-first design
- no authentication
- no search or list tool
- no custom summary output
- no production hardening yet

## Recommended Next Functional Steps

1. Add more study-related tools only when there is a clear consumer need.
2. Decide whether users need a curated summary tool in addition to raw JSON.
3. Define production access rules before broader distribution.
