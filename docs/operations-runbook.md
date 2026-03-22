# Operations Runbook

This document captures the Phase 4 operational guidance for the MCP host.

It covers:

- observability signals
- recommended alert rules
- incident runbooks

## Observability Signals

The MCP host now emits:

- structured request logs with correlation IDs
- request counts and request duration metrics
- auth failure metrics
- rate-limit rejection metrics
- tool invocation count and duration metrics
- study-service request count and duration metrics
- cache hit, miss, and store event metrics
- ASP.NET Core request telemetry
- outbound HTTP telemetry for the configured study service

Correlation header:

- `X-Correlation-Id`

If the caller sends `X-Correlation-Id`, the server echoes it. If not, the server generates one.

## Telemetry Export

Supported modes:

- local logs only
- optional local console OpenTelemetry export
- Azure Monitor / Application Insights export when `Telemetry:ApplicationInsights:ConnectionString` is configured

Local console export:

```text
Telemetry:Console:Enabled = true
```

Azure Monitor export:

```text
Telemetry:ApplicationInsights:ConnectionString = <value>
```

## Recommended Alert Rules

These rules should be implemented in Azure Monitor during platform deployment.

### 1. `5xx` Error Spike

Trigger when:

- MCP `5xx` responses exceed the agreed threshold over a short rolling window

Primary signal:

- request status code metrics or request logs

### 2. Auth Failure Spike

Trigger when:

- `mcp.auth.failures` rises sharply
- or `401`/`403` request volume spikes unexpectedly

Primary signal:

- auth failure metric
- request status code metrics

### 3. Rate-Limit Rejection Spike

Trigger when:

- `mcp.rate_limit.rejections` rises above the baseline
- or `429` responses spike

Primary signal:

- rate-limit rejection metric
- request status code metrics

### 4. Study-Service Failure Or Timeout Spike

Trigger when:

- `mcp.study_service.requests` with failure outcomes rises
- study-service latency exceeds the threshold

Primary signal:

- study-service duration metric
- study-service failure tags

### 5. Readiness Failure

Trigger when:

- `/health/ready` returns unhealthy

Primary signal:

- readiness probe failure
- request logs for `/health/ready`

### 6. SQL Dependency Failure

Trigger when:

- client-registry database health check fails

Primary signal:

- readiness report
- host logs

### 7. Redis Dependency Failure

Trigger when:

- Redis-backed cache or rate limiting is enabled and Redis readiness fails

Primary signal:

- readiness report
- host logs

## Runbooks

### Runbook: Auth Failure Spike

1. Check whether failures are `missing_header`, `invalid_key`, or `inactive_client_or_key`.
2. Confirm whether the affected traffic is expected client traffic or unknown traffic.
3. If a known client is failing, verify the client record and API key status in SQL.
4. Rotate or reissue the client key if compromise or expiry is suspected.
5. If malicious traffic is suspected, reduce exposure at the network edge when available and review rate limits.

## Runbook: `429` Spike

1. Identify whether the spike is global or client-scoped.
2. Check the client code dimension on request and rate-limit metrics.
3. Confirm whether traffic growth is legitimate or abusive.
4. If legitimate, review whether the client override limit is too low.
5. If abusive, keep the lower limit in place and investigate caller origin.

## Runbook: Readiness Failure

1. Call `/health/ready` and inspect the per-check JSON output.
2. If `client_registry_database` is unhealthy, verify SQL connectivity and credentials.
3. If `redis_dependency` is unhealthy, verify Redis connectivity and whether Redis-backed providers are enabled.
4. If `mcp_configuration` is unhealthy, inspect host configuration and secrets settings.
5. Do not treat `ClinicalTrials.Api` reachability as a readiness issue; it is intentionally out of hard readiness.

## Runbook: Study-Service Timeout Or Failure Spike

1. Check study-service latency and failure metrics.
2. Verify that `ClinicalTrials.Api` is healthy and responsive.
3. Review recent changes to `StudyService:Resilience`.
4. If the circuit breaker is opening frequently, inspect upstream stability before increasing retry pressure.
5. If failure is persistent, consider temporary traffic reduction or rollback.

## Runbook: Client Key Rotation

Use the admin CLI:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- rotate-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code <client-code> --key-label rotated
```

Then:

1. deliver the new raw API key securely
2. confirm the client has updated its MCP config
3. verify successful authenticated calls

## Runbook: Client Revocation

Use the admin CLI:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- revoke-key --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --api-key-id <guid>
```

Or deactivate the client:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- deactivate-client --connection-string "$env:CLINICALTRIALS_CLIENTREGISTRY_CONNECTIONSTRING" --client-code <client-code>
```

## Runbook: Rollback

1. identify the most recent known-good deployment
2. redeploy or swap back to the previous version
3. verify `/health/live`
4. verify `/health/ready`
5. run one authenticated MCP smoke test
6. review whether any client-key or configuration changes also need rollback
