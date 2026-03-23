# Documentation Index

This index provides a single entry point for the main documents in this repository.

## Core Documents

- `docs\how_to_run.md`
  - commands to build, run, test, administer, and troubleshoot the API and MCP host
- `docs\client-onboarding.md`
  - process for creating a client, issuing keys, rotating keys, and deactivating access
- `docs\operations-runbook.md`
  - alert catalog, telemetry notes, and operational incident runbooks for the MCP host
- `docs\phase5-infra-design.md`
  - recommended Azure ownership split, Terraform module shape, and shared-vs-owned resource model
- `docs\terraform-runbook.md`
  - operator runbook for Terraform inputs, Azure DevOps pipeline setup, and first Azure deployment
- `docs\production-readiness.md`
  - phased production hardening plan with status, deliverables, and exit criteria

## Operational Assets

- `SQL\001_create_client_registry.sql`
  - SQL schema creation for the MCP client registry
- `SQL\002_seed_local_dev_client.sql`
  - local development seed data for the SQL-backed client registry
- `infra\README.md`
  - Terraform root stack layout, inputs, examples, and deployment workflow for Azure
- `azure-pipelines.yml`
  - Azure DevOps CI/CD pipeline for build, container push, Terraform apply, slot smoke test, and slot swap
- `Dockerfile`
  - production container image definition for `ClinicalTrials.Mcp`
- `ClinicalTrials.Admin`
  - admin CLI for client creation, key issuance, rotation, revocation, and deactivation

## MCP Documents

- `docs\mcp-server.md`
  - quick MCP server overview, endpoints, and local MCP client setup
- `docs\mcp_tech.md`
  - technical architecture, hosting, transport, configuration, and runtime behavior
- `docs\mcp_func.md`
  - functional behavior, user-facing expectations, and acceptance scenarios

## API Document

- `docs\studies-controller-endpoints.md`
  - REST endpoint details for the `StudiesController`
