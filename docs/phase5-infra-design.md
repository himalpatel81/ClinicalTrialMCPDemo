# Phase 5 Infrastructure Design

This document describes the recommended Azure and Terraform design for Phase 5.

## Goal

Deploy the MCP service using Terraform without forcing this repository to take ownership of shared Azure resources that already exist in the subscription.

The preferred model is:

- reference shared platform resources
- manage MCP-specific application resources in this repository

## Implementation Snapshot

The repository now contains the Phase 5 delivery assets:

- [Dockerfile](/e:/GitHimal/ClinicalTrialMCPDemo/Dockerfile)
- [azure-pipelines.yml](/e:/GitHimal/ClinicalTrialMCPDemo/azure-pipelines.yml)
- [infra/README.md](/e:/GitHimal/ClinicalTrialMCPDemo/infra/README.md)
- [providers.tf](/e:/GitHimal/ClinicalTrialMCPDemo/infra/providers.tf)
- [main.tf](/e:/GitHimal/ClinicalTrialMCPDemo/infra/main.tf)
- [variables.tf](/e:/GitHimal/ClinicalTrialMCPDemo/infra/variables.tf)
- [outputs.tf](/e:/GitHimal/ClinicalTrialMCPDemo/infra/outputs.tf)

The Terraform shape is implemented as one root stack with reusable submodules and example environment input files.

## Naming Convention Policy

For Azure infrastructure created and managed by this repository:

- use `airydocs` in resource names
- do not use `clinicaltrials` in Azure resource names

This rule applies to:

- resource groups
- Web App names
- managed identity names
- monitoring resource names
- any other app-owned Azure resource created by this stack

Important:

- existing shared resources keep their current names
- referenced platform resources are not renamed by this plan
- the `airydocs` naming rule applies only to new or repo-managed Azure resources

## Ownership Model

### Shared Platform Resources

These are expected to already exist and may be shared by multiple applications:

- Azure Container Registry
- App Service Plan
- SQL Server
- Redis cache
- Key Vault
- optionally shared Application Insights or Log Analytics

### MCP App Resources

These should usually be owned by this repository:

- dedicated MCP resource group per environment
- MCP Web App
- staging slot
- managed identity
- app settings and configuration bindings
- Key Vault access configuration for this app
- optional dedicated SQL database for this app
- optional app-specific Application Insights resource if telemetry should be isolated

## Ownership Diagram

```text
+---------------------------------------------------------------+
|                    Shared Platform Resources                  |
|                  existing in subscription already             |
|                                                               |
|  +--------+   +------------------+   +--------------------+   |
|  |  ACR   |   | App Service Plan |   |     SQL Server     |   |
|  +--------+   +------------------+   +--------------------+   |
|                                                               |
|  +--------+   +------------------+   +--------------------+   |
|  | Redis  |   |    Key Vault     |   | App Insights/LA ?  |   |
|  +--------+   +------------------+   +--------------------+   |
+----------------------------^----------------------------------+
                             |
                             | references / bindings
                             |
+----------------------------+----------------------------------+
|                 MCP App Resources Owned By Repo              |
|                                                               |
|  +--------------------+      +----------------------------+   |
|  |   AiryDocs MCP    |----->| App Settings / Conn Strings|   |
|  |     Web App        |      | / telemetry config         |   |
|  +--------------------+      +----------------------------+   |
|            |                                                  |
|            v                                                  |
|  +--------------------+                                       |
|  |   Staging Slot     |                                       |
|  +--------------------+                                       |
|            |                                                  |
|            v                                                  |
|  +--------------------+      +----------------------------+   |
|  | Managed Identity   |----->| KV access / RBAC bindings  |   |
|  +--------------------+      +----------------------------+   |
|                                                               |
|  +--------------------+                                       |
|  | SQL Database ?     |  optional dedicated DB for MCP app   |
|  +--------------------+                                       |
+---------------------------------------------------------------+
```

## Recommended Terraform Shape

Use one root stack plus multiple Terraform modules, not one flat script.

Implemented layout:

```text
infra/
  .terraform.lock.hcl
  providers.tf
  variables.tf
  main.tf
  outputs.tf
  README.md
  modules/
    mcp_web_app/
    mcp_database/
    mcp_monitoring_binding/
    mcp_container_app_compat/
  environments/
    dev/
    staging/
    prod/
```

The root stack composes:

- `mcp_web_app`
- `mcp_database`
- `mcp_monitoring_binding`
- `mcp_container_app_compat`

Suggested naming examples for repo-managed Azure resources:

- resource group: `rg-airydocs-mcp-dev`
- resource group: `rg-airydocs-mcp-staging`
- resource group: `rg-airydocs-mcp-prod`
- Web App: `app-airydocs-mcp-dev`
- Web App: `app-airydocs-mcp-prod`
- managed identity: `id-airydocs-mcp-prod`
- Application Insights: `appi-airydocs-mcp-prod`

The dedicated MCP resource group rule is:

- all repo-owned MCP Azure resources go into `rg-airydocs-mcp-<env>` unless an override is explicitly supplied
- shared platform resources remain in their current resource groups and are only referenced

## Recommended Handling For Existing Azure Resources

Use a hybrid approach.

### Use Existing Resources By Reference

Prefer `data` lookups or explicit input IDs for:

- ACR
- App Service Plan
- SQL Server
- Redis
- Key Vault
- shared monitoring resources

This keeps Terraform from managing the lifecycle of resources that are already shared.

It also means those shared resources can keep their existing names even if they do not follow the `airydocs` naming convention.

### Create MCP-Specific Resources In Terraform

Create and manage:

- Web App
- deployment slot
- managed identity
- app-specific RBAC or Key Vault access
- app-specific configuration
- optional dedicated SQL database

## Import Rule

Only import an existing Azure resource into Terraform state if this repository should become the long-term owner of that resource.

If the resource is shared across teams or applications, do not import it into this app stack. Reference it instead.

## Environment Inputs

Each environment root should accept inputs such as:

- existing ACR resource ID or name
- existing App Service Plan ID
- existing SQL Server ID or name
- existing Redis ID or host
- existing Key Vault ID or name
- existing monitoring resource ID or connection string
- flags for optional creation of MCP-specific database or telemetry resources

Example intent:

```text
use existing ACR
use existing App Service Plan
use existing SQL Server
use existing Redis
use existing Key Vault
create MCP Web App
create MCP staging slot
create MCP managed identity
create MCP-specific SQL database if needed
```

## Decision Rules

- if a resource is shared across multiple applications, reference it
- if a resource is specific to this MCP service, manage it here
- if ownership is unclear, do not import it until ownership is agreed

## Advantages Of This Model

- lower risk of accidental changes to shared platform services
- cleaner Terraform state ownership
- easier adoption in subscriptions with pre-existing resources
- easier future movement between App Service and Container Apps because the app boundary stays explicit

## Main Concern

The main risk is ownership ambiguity.

If shared resources are partly managed manually and partly managed by this app stack, drift and accidental changes become more likely.

To avoid that:

- define ownership per resource before implementation
- document which resources are referenced versus managed
- keep app-specific infrastructure separate from platform-shared infrastructure

## Recommendation

For this repository:

- do not make this stack own the whole shared platform
- make this stack own the MCP application deployment surface
- bind it to existing shared Azure resources through inputs and data sources
- use `airydocs` for all newly created Azure resource names owned by this stack
- keep the Terraform root in `infra/` and the environment-specific values outside source control except for checked-in examples
