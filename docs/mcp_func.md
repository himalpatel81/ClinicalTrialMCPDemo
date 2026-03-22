# MCP Functional Documentation

## Purpose

This document describes the functional behavior of the MCP server from a consumer perspective.

## Functional Summary

The server provides:

- one protected MCP tool endpoint
- one MCP tool for study lookup
- two HTTP health endpoints
- SQL-backed API key access control
- study lookup caching
- per-client and global rate limiting
- request timeout and request-size protections

Current scope:

- retrieve one study by NCT ID
- return a readable summary plus raw study JSON on success
- return structured error information on failure
- route the lookup through the configured study service instead of calling ClinicalTrials.gov directly

Out of scope:

- search across studies
- list studies
- create or modify study data

## Available MCP Tool

### `get_study_by_nct_id`

Functional purpose:

- retrieve the study record for a single NCT ID

Input:

- `nctId` as text

## Functional Access Rules

### Rule 1: Authenticated MCP Access

The MCP endpoint requires a valid API key that exists in the SQL-backed client registry.

Functional meaning:

- clients must send `X-Api-Key`
- local development uses `clinical-trials-local-dev-key` after the SQL seed script is applied
- missing or invalid keys are rejected before tool execution
- inactive, revoked, or expired keys are rejected

### Rule 2: Indirect Upstream Access

The MCP server does not call ClinicalTrials.gov directly.

Functional meaning:

- MCP calls the configured study service
- local development defaults that study service to `ClinicalTrials.Api`

### Rule 3: Read-Only Behavior

The tool is read-only.

### Rule 4: Structured Success Output

Successful responses contain:

- readable summary text in visible tool content
- raw study JSON in structured content

### Rule 5: Structured Error Output

Tool failures return structured error metadata.

### Rule 6: Rate Limiting

The MCP endpoint enforces:

- global rate limit
- per-client rate limit

Limit exhaustion returns HTTP `429`.

## Functional Scenarios

### Scenario 1: Valid NCT ID

Expected outcome:

- authenticated client receives summary content and raw study JSON

### Scenario 2: Missing API Key

Expected outcome:

- request is rejected with HTTP `401`

### Scenario 3: Revoked Or Inactive Client Key

Expected outcome:

- request is rejected with HTTP `403`

### Scenario 4: Empty Input

Expected outcome:

- tool returns `400`

### Scenario 5: Study Not Found

Expected outcome:

- tool returns structured `404`
- short negative cache may serve repeated `404` lookups without another upstream call

### Scenario 6: Upstream Service Unreachable

Expected outcome:

- tool returns structured `502`

### Scenario 7: Upstream Timeout

Expected outcome:

- tool returns structured `504`

### Scenario 8: Rate Limit Exceeded

Expected outcome:

- MCP request returns HTTP `429`

### Scenario 9: Oversized MCP Request

Expected outcome:

- MCP request is rejected with HTTP `413`

### Scenario 10: Internal Dependency Readiness Failure

Expected outcome:

- `/health/ready` returns a non-success status when required internal dependencies are unavailable

## Health Endpoints

### `GET /health/live`

Confirms that the MCP host process is running.

### `GET /health/ready`

Confirms that required Phase 3 configuration and internal dependencies are ready.

It does not confirm:

- live study-service reachability

## Local Functional Expectations

Local MCP execution requires:

1. SQL schema script applied
2. local dev seed script applied
3. `ConnectionStrings__ClientRegistry` set
4. `ClinicalTrials.Api` running
5. `ClinicalTrials.Mcp` running

Local profile behavior:

- SQL-backed auth
- in-memory cache
- in-memory rate limiting
- no local Redis dependency
- live readiness check against local SQL

## Acceptance Criteria

The implementation is functionally correct when:

- `/health/live` returns `200 OK`
- `/health/ready` returns `200 OK`
- missing or invalid keys return `401`
- inactive or revoked keys return `403`
- valid NCT IDs return summary text plus structured JSON
- repeated successful lookups can be served from cache
- repeated `404` lookups can be served from negative cache
- per-client rate-limit exhaustion returns `429`
- oversized requests return `413`

## Current Constraints

- one MCP tool only
- no search or list tool
- readiness still does not prove live study-service reachability
