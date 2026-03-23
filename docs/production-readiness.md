# Production Readiness

## Summary

This document captures the phased implementation plan for making the ClinicalTrials MCP service internet-facing, secure, robust, efficient, and operationally ready for production use.

The service is suitable for development and early hardening work, but it is not yet ready for public production deployment. Local runtime remains a hard requirement throughout all phases, and local runtime must not depend on Azure-managed services.

## Local-First Runtime Requirement

Two supported runtime profiles are assumed:

- `Local`
  - no Azure dependency
  - local secrets from environment variables, user-secrets, or committed local-only dev config
  - local SQL Server-compatible database in later phases
  - in-memory cache and in-memory rate limiting in later phases
  - local seeded dev API key flow
  - local console and debug telemetry only
- `Production`
  - Azure-hosted runtime
  - Azure SQL
  - Azure Cache for Redis
  - Key Vault
  - Azure Monitor / Application Insights

The service must stay runnable end-to-end on a developer machine without Azure Key Vault, Azure SQL, Azure Redis, Azure Monitor, or any other Azure runtime dependency.

## Phase Status Overview

| Phase | Title | Status | Outcome |
| --- | --- | --- | --- |
| 0 | Current Baseline | Completed | MCP tool works in dev, stateless mode is enabled, docs and tests exist |
| 1 | Security Foundation | Completed | API key auth, protected `/mcp`, split health endpoints, local dev key flow, and production-safe host defaults are in place |
| 2 | Data Plane Hardening | Completed | SQL-backed client registry, hashed API keys, local-memory and Redis-capable rate limiting/caching, admin CLI, and checked-in SQL scripts are in place |
| 3 | Runtime Resilience | Completed | HTTP resilience, request protections, deeper readiness checks, and safer host failure handling are in place |
| 4 | Observability And Ops | Completed | Correlation-aware logs, OpenTelemetry metrics and traces, Azure Monitor integration hooks, alert catalog, and operational runbooks are in place |
| 5 | Deployment Platform | Completed | Dockerfile, Terraform stack, env examples, Azure DevOps pipeline, App Service deployment contract, and Container Apps compatibility assets are in repo |
| 6 | Release Validation | Pending | Security review, load validation, smoke tests, rollback readiness, and production cutover |

## Implementation Phases

### Phase 0: Current Baseline

**Status:** `Completed`

Deliverables:

- MCP host exposed at `/mcp`
- stateless streamable HTTP transport enabled
- one read-only tool: `get_study_by_nct_id`
- shared lookup service used by REST and MCP
- local run docs and tests

### Phase 1: Security Foundation

**Status:** `Completed`

Delivered:

- API key authentication middleware using `X-Api-Key`
- authorization required on `/mcp`
- documented local development authentication path using a seeded dev API key
- permissive CORS removed from the default host pipeline
- allowed-host defaults tightened to local addresses
- forwarded-header handling made available behind configuration
- split health endpoints into `/health/live` and `/health/ready`
- startup validation for required auth and upstream configuration
- placeholder configuration contracts added for SQL, Redis, secrets, telemetry, and rate limiting
- local runtime preserved without any Azure dependency

Implementation notes:

- API key authentication was introduced in this phase and is now backed by the SQL client registry from Phase 2
- local development uses the seeded `clinical-trials-local-dev-key`
- shared and production environments are expected to use admin-issued keys from the client registry

Exit criteria:

- unauthenticated requests to `/mcp` fail
- invalid API keys fail deterministically
- disabled clients are rejected
- only intended endpoints remain anonymously accessible
- local developers can still run the MCP server with a documented authenticated workflow
- local startup does not require Azure login or any Azure service dependency

### Phase 2: Data Plane Hardening

**Status:** `Completed`

Delivered:

- create Azure SQL-backed client registry for known clients
- create checked-in SQL scripts under `SQL/` for database creation and database seeding
- do not implement database creation or seed execution in application startup code
- store only API key hashes and metadata, never raw keys after issuance
- model client status, key status, created, rotated, revoked timestamps, and usage policy
- add admin-only operational flow for create, rotate, revoke, and deactivate
- add Azure Redis-backed per-client and global rate limiting
- add Redis-backed short TTL response caching for successful lookups
- optionally add short negative-cache TTL for repeat `404` lookups
- use the existing local SQL Server environment for local development
- use in-memory cache and in-memory rate limiting for local development instead of local Redis
- document the split between local in-memory behavior and production Redis-backed behavior
- add an admin-only console flow for create client, issue key, rotate key, revoke key, and deactivate client

Exit criteria:

- approved clients can be stored outside appsettings
- revoked keys stop working immediately
- per-client and global limits work across multiple instances
- repeated successful lookups reduce upstream dependency calls
- local development can still run end-to-end with local SQL plus in-memory cache/rate limiting only
- required schema and seed data can be created from checked-in SQL scripts in `SQL/`

Implementation notes:

- client registry schema is delivered in `SQL/001_create_client_registry.sql`
- local dev seed data is delivered in `SQL/002_seed_local_dev_client.sql`
- runtime auth now validates hashed API keys against SQL
- local development uses SQL Server plus in-memory cache and in-memory rate limiting
- production configuration remains Redis-capable for cache and rate limiting
- admin operations are implemented as a separate CLI project, not a public HTTP surface

### Phase 3: Runtime Resilience

**Status:** `Completed`

Delivered:

- add official .NET HTTP resilience policies for the configured study-service dependency
- configure total timeout, attempt timeout, bounded retry, and circuit-breaker behavior
- add MCP request timeout and request-body-size protections
- add safer host-level exception handling for malformed or oversized requests
- extend readiness checks for live SQL connectivity
- extend readiness checks for live Redis connectivity when Redis-backed providers are enabled
- extend readiness checks for required secrets configuration
- keep the study-service dependency out of hard readiness

Implementation notes:

- study-service HTTP resilience now uses `Microsoft.Extensions.Http.Resilience`
- request protection is configuration-driven through `RequestProtection`
- `/health/ready` now validates configuration plus internal dependency availability
- readiness still intentionally excludes `ClinicalTrials.Api` reachability
- local runtime keeps SQL plus in-memory cache and in-memory rate limiting
- local runtime still does not require Azure services or local Redis

Exit criteria:

- transient upstream failures do not immediately fail the service
- persistent upstream failures degrade gracefully
- readiness reflects internal dependency availability
- malformed or abusive requests are rejected safely

### Phase 4: Observability And Ops

**Status:** `Completed`

Delivered:

- integrate OpenTelemetry-based tracing and metrics into the MCP host
- add optional Azure Monitor / Application Insights export through configuration
- emit structured request logs with correlation IDs
- track request rate, tool latency, upstream latency, auth failures, `429`s, `5xx`s, cache events, and per-client usage
- keep authentication and request logs free of raw API key values
- add a checked-in alert catalog for auth spikes, `429` spikes, `5xx` spikes, readiness failure, upstream degradation, and SQL or Redis issues
- add checked-in operational runbooks for key rotation, revocation, rollback, and dependency outage handling
- keep local telemetry non-Azure-dependent

Implementation notes:

- custom observability is implemented under `ClinicalTrials.Mcp/Observability`
- Azure Monitor export is opt-in through `Telemetry:ApplicationInsights:ConnectionString`
- local console OpenTelemetry export is opt-in through `Telemetry:Console:Enabled`
- actual Azure alert resource deployment remains part of Phase 5 infrastructure work, but the alert definitions are now documented in-repo

Exit criteria:

- operators can identify who called the service, what happened, and why it failed
- alerts are actionable and mapped to documented runbooks
- local developers can debug the service without Azure Monitor or Application Insights

### Phase 5: Deployment Platform

**Status:** `Completed`

Detailed design reference:

- `docs\phase5-infra-design.md`

Deliverables:

- containerize the MCP app with a checked-in `Dockerfile` and `.dockerignore`
- create a Terraform root stack under `infra/` with reusable modules for:
  - MCP Web App
  - deployment slot
  - managed identity and app access bindings
  - optional MCP-specific SQL database
  - optional Application Insights binding
  - optional non-prod Container Apps compatibility deployment
- reference existing shared Azure resources through inputs instead of taking ownership of them:
  - Azure Container Registry
  - App Service Plan
  - SQL Server
  - Redis
  - Key Vault
  - optionally shared Log Analytics / Application Insights
- create Azure DevOps YAML pipeline for build, test, image push, Terraform deploy, slot smoke test, and slot swap
- add environment example inputs for `dev`, `staging`, and `prod`
- keep the deployment contract dual-ready for Azure App Service first and Azure Container Apps later
- preserve a first-class local run path
- use `airydocs` as the naming convention for Azure resources created by this repository while continuing to reference pre-existing shared resources by their current names

Implementation notes:

- repo-owned Azure resources are placed in dedicated MCP resource groups such as `rg-airydocs-mcp-dev`, `rg-airydocs-mcp-staging`, and `rg-airydocs-mcp-prod`
- shared platform resources remain in their existing resource groups and are passed in by ID or connection details
- the root Terraform stack lives under `infra/`, with example environment inputs under `infra/environments/`
- the Azure DevOps pipeline lives in `azure-pipelines.yml`
- local verification now includes `terraform validate` for the checked-in Terraform stack and `docker build` for the checked-in container image contract
- live Azure apply, slot swap, and non-prod environment rollout validation remain part of Phase 6 release validation

Exit criteria:

- checked-in Terraform configuration validates successfully
- checked-in container image contract builds successfully
- environment-specific shared resource references stay externalized through Terraform inputs and pipeline variables
- App Service remains the primary deployment target
- Container Apps compatibility is available without changing app code
- local run instructions remain valid after platform changes

### Phase 6: Release Validation

**Status:** `Pending`

Deliverables:

- run end-to-end security validation
- run load and concurrency tests with Redis-backed rate limiting enabled
- validate MCP client interoperability for approved clients
- perform deployment smoke tests on the staging slot
- validate rollback and secret rotation procedures
- finalize production checklist and cutover plan

Exit criteria:

- approved MCP clients can authenticate and call the tool successfully
- unauthorized traffic is blocked
- quotas and caching behave correctly under load
- observability and rollback are proven before public launch

## Test Plan By Phase

- Phase 1:
  - missing API key returns `401`
  - invalid key returns `401`
  - disabled client returns `403`
  - `/health/live` remains public
  - `/health/ready` remains public
- Phase 2:
  - SQL-backed key lookup works
  - checked-in SQL scripts create the required schema and seed data
  - hashed keys validate correctly
  - per-client and global rate limits return `429`
  - Redis cache reduces duplicate upstream calls
  - local profile works with in-memory cache and in-memory rate limiting
- Phase 3:
  - timeout, retry, and circuit-breaker policies behave as configured
  - `/health/ready` fails when SQL, Redis, or secrets are unavailable
  - malformed or oversized requests are rejected safely
- Phase 4:
  - telemetry includes correlation and client identity metadata
  - logs redact secrets
  - alert rules fire on simulated failures
- Phase 5:
  - `terraform validate` succeeds for the checked-in stack
  - Docker image builds successfully from the checked-in `Dockerfile`
  - pipeline definition covers image build, Terraform apply, slot smoke test, and slot swap
  - same image contract can target App Service and the optional compatibility Container App
- Phase 6:
  - load test passes
  - rollback test passes
  - approved external client interoperability is validated

## Assumptions And Defaults

- current repo state includes completed phase 1 hardening, not final production readiness
- internet-facing first release is MCP only; REST remains non-public
- App Service is the first production host; Container Apps compatibility is built in and validated in non-prod
- API keys are the chosen v1 auth model for known clients
- Azure SQL is the production system of record for client and key metadata
- Azure Redis is the production backing store for cross-instance rate limiting and caching
- local development will use the existing local SQL Server environment and will not require local Redis
- database creation and seeding will be delivered as checked-in SQL scripts under `SQL/`, not as startup code or auto-migrations
- Terraform and Azure DevOps pipelines are in scope
- production go-live is gated on completion of phases 5 through 6
- the MCP server must remain runnable locally throughout the hardening effort
- local runtime must not require any Azure-managed service

## Local Development Concern

The main concern is not authentication itself. The real risk is accidentally coupling the service to Azure-only runtime assumptions with no documented local bootstrap path.

To avoid that:

- local developers must always have a documented way to obtain or seed a dev API key
- local execution must support the future SQL dependency through local setup, not Azure
- local execution must use in-memory cache and rate limiting instead of requiring local Redis
- secret resolution must support local configuration in addition to production secret stores
- Key Vault must remain production-only
- Azure Monitor and Application Insights must remain production-only
- local run and local MCP client docs must stay current as hardening work continues
