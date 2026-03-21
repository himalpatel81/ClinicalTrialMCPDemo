# MCP Technical Documentation

## Purpose

This document describes the technical design and implementation of the HTTP MCP server in this repository.

## Solution Overview

The implementation is split into four application layers:

1. `ClinicalTrials.Api`
   - REST API host
   - exposes `GET /api/studies/{nctId}`
   - calls ClinicalTrials.gov through the shared lookup service
2. `ClinicalTrials.Mcp`
   - HTTP MCP host
   - exposes `/mcp`, `/health/live`, and `/health/ready`
   - authenticates with a SQL-backed client registry
   - rate limits and caches study lookups
3. `ClinicalTrials.Admin`
   - console CLI for client and API key lifecycle
   - no public HTTP admin surface
4. `ClinicalTrials.Shared`
   - shared ClinicalTrials.gov lookup service used by the REST API

The runtime flow is:

1. MCP client calls `ClinicalTrials.Mcp`
2. `ClinicalTrials.Mcp` authenticates the caller against SQL Server
3. `ClinicalTrials.Mcp` rate limits the request
4. `ClinicalTrials.Mcp` checks its study lookup cache
5. cache miss calls `ClinicalTrials.Api`
6. `ClinicalTrials.Api` calls ClinicalTrials.gov

## Technology Stack

- .NET `net10.0`
- ASP.NET Core
- `ModelContextProtocol.AspNetCore` `1.1.0`
- `Microsoft.Data.SqlClient`
- `StackExchange.Redis`
- `Microsoft.Extensions.Caching.StackExchangeRedis`
- xUnit

## MCP Host Architecture

### Core Responsibilities

- host MCP over stateless streamable HTTP
- protect `/mcp` with SQL-backed API key authentication
- expose anonymous liveness and readiness endpoints
- call a configurable study service over HTTP
- cache lookup responses
- enforce per-client and global rate limits
- preserve a non-Azure local runtime path

### Key Startup Behavior

[Program.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Program.cs) now:

- validates study service settings
- validates API key header settings
- validates client registry connection string presence
- validates cache and rate-limit settings
- registers SQL-backed `IClientRegistryStore`
- registers memory or Redis cache based on configuration
- registers memory or Redis rate-limit storage based on configuration
- registers the `ApiKey` auth scheme and active-key policy
- registers the study lookup HTTP client and caching decorator
- maps:
  - `GET /health/live`
  - `GET /health/ready`
  - `POST/GET /mcp`

## Authentication Design

### Header

- request header: `X-Api-Key`

### Registry Backing Store

Authentication no longer uses config-backed raw keys.

It now uses:

- [SqlClientRegistryStore.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Data/SqlClientRegistryStore.cs)
- SQL schema in [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql)
- local seed in [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql)

### Hashing

Raw API keys are never stored in the database.

[ApiKeyProtector.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Authentication/ApiKeyProtector.cs) uses:

- PBKDF2-SHA512
- random 32-byte salt
- 120,000 iterations
- deterministic lookup prefix

The auth flow is:

1. compute a lookup prefix from the supplied raw key
2. load candidate keys from SQL by prefix
3. verify the supplied raw key against the stored salted hash
4. emit claims for:
   - client code
   - client active status
   - API key active status
   - optional permit-limit override

### Authorization

The MCP authorization policy now requires:

- authenticated user
- `clinicaltrials.client_active = True`
- `clinicaltrials.api_key_active = True`

Invalid keys return `401`.
Inactive or revoked clients or keys return `403`.

## Data Model

The client registry schema uses two tables:

- `dbo.McpClients`
- `dbo.McpClientApiKeys`

Modeled fields include:

- client code
- display name
- active/inactive status
- created and updated timestamps
- deactivated timestamp
- per-client permit-limit override
- API key label
- key prefix
- key hash material
- key active/inactive status
- created timestamp
- revoked timestamp
- expiration timestamp
- replacement key linkage

## Admin Operations

Admin operations are implemented by:

- [ClientRegistryAdminService.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Administration/ClientRegistryAdminService.cs)
- [ClinicalTrials.Admin.csproj](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Admin/ClinicalTrials.Admin/ClinicalTrials.Admin.csproj)

Supported CLI operations:

- create client
- issue API key
- rotate API key
- revoke API key
- deactivate client

This keeps admin operations out of the public MCP host.

## Study Service Routing

The MCP host depends on:

- [IStudyLookupEndpointClient.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Services/IStudyLookupEndpointClient.cs)
- [StudyServiceHttpClient.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Services/StudyServiceHttpClient.cs)

Current local default:

- `StudyService:BaseUrl = http://localhost:5010/`
- `StudyService:StudyLookupPathTemplate = api/studies/{nctId}`

This keeps the MCP host independent from the direct ClinicalTrials.gov integration and makes future replacement of `ClinicalTrials.Api` a lower-disruption change.

## Caching Design

Caching is implemented by:

- [CachedStudyLookupEndpointClient.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Caching/CachedStudyLookupEndpointClient.cs)
- [MemoryStudyLookupCacheStore.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Caching/MemoryStudyLookupCacheStore.cs)
- [DistributedStudyLookupCacheStore.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/Caching/DistributedStudyLookupCacheStore.cs)

Behavior:

- cache successful `2xx` lookups
- cache `404` lookups when negative caching is enabled
- do not cache request failures or timeouts

Providers:

- local: memory
- production: Redis

## Rate Limiting Design

Rate limiting is implemented by:

- [McpRateLimitingMiddleware.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/RateLimiting/McpRateLimitingMiddleware.cs)
- [MemoryMcpRateLimitStore.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/RateLimiting/MemoryMcpRateLimitStore.cs)
- [RedisMcpRateLimitStore.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Mcp/ClinicalTrials.Mcp/RateLimiting/RedisMcpRateLimitStore.cs)

Behavior:

- global fixed-window rate limit
- per-client fixed-window rate limit
- optional per-client permit-limit override from SQL
- HTTP `429` on limit exhaustion

Providers:

- local: memory
- production: Redis

## Local Development Profile

Local development now uses:

- SQL Server for the client registry
- in-memory cache
- in-memory rate limiting
- seeded dev key `clinical-trials-local-dev-key`
- no local Redis requirement

## Testing Strategy

The current test suite covers:

- API key hashing and verification
- cached study lookup behavior
- study service client path construction
- admin service client and key lifecycle logic
- authenticated MCP access
- inactive client rejection
- per-client rate limiting
- MCP tool discovery and invocation

The current suite passes with 32 tests.

## Current Technical Limits

- readiness still validates configuration, not live SQL or study-service connectivity
- no upstream HTTP resilience policies yet
- no structured correlation or audit logging yet
- no deployment automation yet

## Recommended Next Technical Steps

1. Add upstream resilience policies and deeper readiness checks.
2. Add structured correlation and audit logging.
3. Add production telemetry and alerting.
4. Add deployment automation and platform assets.
