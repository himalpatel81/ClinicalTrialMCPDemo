# Infrastructure

This folder contains the Phase 5 infrastructure assets for the internet-facing MCP deployment.

## Structure

- `providers.tf`
  - shared Terraform provider and backend definition
- `variables.tf`
  - root stack inputs
- `main.tf`
  - root stack composition for the MCP platform
- `outputs.tf`
  - deployment outputs used by the pipeline
- `.terraform.lock.hcl`
  - provider lock file for repeatable Terraform init behavior
- `modules/`
  - reusable Terraform modules for app-owned Azure resources
- `environments/`
  - example input files and backend examples for `dev`, `staging`, and `prod`

## Ownership Model

This stack is designed to:

- create MCP-owned Azure resources
- reference existing shared platform resources

New repo-managed Azure resource names use the `airydocs` convention.

Repo-owned MCP resources are expected to live in dedicated resource groups such as:

- `rg-airydocs-mcp-dev`
- `rg-airydocs-mcp-staging`
- `rg-airydocs-mcp-prod`

Shared resources keep their current names and current resource groups.

## Root Modules

The root stack composes these reusable modules:

- `modules/mcp_web_app`
  - Web App, staging slot, user-assigned identity, ACR pull role, and optional Key Vault access
- `modules/mcp_database`
  - optional MCP-specific Azure SQL database
- `modules/mcp_monitoring_binding`
  - optional Application Insights resource or binding to an existing connection string
- `modules/mcp_container_app_compat`
  - optional non-prod compatibility deployment for Azure Container Apps

## Expected Inputs

The root stack expects inputs for:

- environment name
- Azure region
- existing App Service Plan ID
- existing ACR ID and login server
- existing Key Vault ID and URI when secret references are used
- existing SQL Server ID when an MCP-specific database should be created
- optional existing or new Application Insights wiring
- study-service base URL
- client-registry and Redis connection configuration

These values are intentionally externalized. The checked-in environment files under `environments/` are examples only.

## Secret Strategy

For App Service:

- prefer Key Vault secret URIs for connection strings
- the stack renders App Service Key Vault references into app settings

For optional Container Apps compatibility validation:

- Key Vault secret IDs can be passed to the compatibility module
- if raw values are used instead, those values become part of Terraform state

## Environment Examples

Each environment folder contains:

- `<env>.auto.tfvars.example`
- `backend.hcl.example`

These are examples only. They should be copied and adapted outside source control for real deployments.

## Typical Local Workflow

1. Install Terraform.
2. Copy the appropriate environment example values.
3. Supply backend configuration.
4. Run:

```powershell
terraform -chdir=infra init -backend-config="environments/dev/backend.hcl"
terraform -chdir=infra plan -var-file="environments/dev/dev.auto.tfvars"
```

## CI/CD Strategy

The repository includes [azure-pipelines.yml](/e:/GitHimal/ClinicalTrialMCPDemo/azure-pipelines.yml).

The pipeline expects:

- an Azure service connection
- a Docker registry service connection
- Terraform backend settings
- a variable named `terraformVariablesJson` containing the root stack input values as JSON

The pipeline then:

- builds and tests the code
- builds and pushes the container image
- writes `terraform.auto.tfvars.json`
- runs Terraform init, validate, plan, and apply
- deploys the new image to the staging slot
- smoke tests `/health/ready`
- swaps the staging slot into production

## Database Note

If the MCP-specific Azure SQL database is created by Terraform, the database schema is still applied separately using the checked-in SQL scripts under [SQL](/e:/GitHimal/ClinicalTrialMCPDemo/SQL). Terraform does not create tables or seed data.

## Important Note

Terraform is not installed in this repository by default. Validation and apply must run in a machine or pipeline that has Terraform available.
