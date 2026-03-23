# Terraform Runbook

This runbook describes the next actions to deploy the Phase 5 infrastructure and application assets to Azure.

It is written against the current repo state:

- Terraform root stack: [main.tf](/e:/GitHimal/ClinicalTrialMCPDemo/infra/main.tf)
- Terraform docs: [README.md](/e:/GitHimal/ClinicalTrialMCPDemo/infra/README.md)
- Pipeline: [azure-pipelines.yml](/e:/GitHimal/ClinicalTrialMCPDemo/azure-pipelines.yml)
- Container image: [Dockerfile](/e:/GitHimal/ClinicalTrialMCPDemo/Dockerfile)

## Scope

This runbook assumes:

- shared Azure resources already exist and are not owned by this repo
- this repo will create only app-owned `airydocs` MCP resources
- Azure App Service is the primary deployment target
- Azure Container Apps validation is optional and non-prod only

Shared resources expected to already exist:

- Azure Container Registry
- App Service Plan
- SQL Server
- Redis
- Key Vault
- optionally Log Analytics / Application Insights

## Recommended Execution Order

1. Confirm the shared resource inventory for the target environment.
2. Create the Terraform state backend.
3. Prepare Key Vault secrets and connection strings.
4. Prepare the environment-specific Terraform values.
5. Create Azure DevOps service connections.
6. Create Azure DevOps variable groups.
7. Create the Azure DevOps pipeline from the checked-in YAML.
8. Run the first `dev` deployment manually.
9. Apply the client-registry SQL schema.
10. Bootstrap at least one API client key.
11. Verify MCP health and a real tool call.
12. Repeat for `staging`, then `prod`.

## Step 1: Confirm Shared Azure Resources

For each environment, collect the actual values for:

- App Service Plan resource ID
- ACR resource ID
- ACR login server
- Key Vault resource ID
- Key Vault URI
- SQL Server resource ID
- Redis connection string secret URI or raw connection string
- Log Analytics workspace ID if used
- the upstream study service base URL

Important:

- the MCP server now calls `ClinicalTrials.Api` or another configured study service
- `study_service_base_url` must point at that service, not ClinicalTrials.gov

## Step 2: Create the Terraform State Backend

The Terraform backend is `azurerm`, so the backend storage must exist before `terraform init`.

Create or reuse:

- one resource group for Terraform state
- one storage account
- one blob container

The checked-in backend examples are:

- [backend.hcl.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/dev/backend.hcl.example)
- [backend.hcl.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/staging/backend.hcl.example)
- [backend.hcl.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/prod/backend.hcl.example)

Each environment needs a real backend file with values for:

- `resource_group_name`
- `storage_account_name`
- `container_name`
- `key`

Recommended state keys:

- `airydocs-mcp-dev.tfstate`
- `airydocs-mcp-staging.tfstate`
- `airydocs-mcp-prod.tfstate`

## Step 3: Prepare Key Vault Secrets

Prefer Key Vault secret URIs over raw connection strings in Terraform variables.

At minimum, prepare secrets for:

- client registry SQL connection string
- Redis connection string

Recommended practice:

- keep raw secrets out of `terraformVariablesJson`
- put secrets in Key Vault first
- pass the secret URIs to Terraform

The Terraform stack expects:

- `client_registry_connection_string_secret_uri`
- `redis_connection_string_secret_uri`

If you choose to pass raw values instead, use:

- `client_registry_connection_string`
- `redis_connection_string`

That works, but it is weaker operationally because raw values may end up in more places than necessary.

## Step 4: Prepare Environment Terraform Values

Use the checked-in examples as the starting point:

- [dev.auto.tfvars.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/dev/dev.auto.tfvars.example)
- [staging.auto.tfvars.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/staging/staging.auto.tfvars.example)
- [prod.auto.tfvars.example](/e:/GitHimal/ClinicalTrialMCPDemo/infra/environments/prod/prod.auto.tfvars.example)

The pipeline does not read these files directly. It expects a JSON variable named `terraformVariablesJson`.

Minimum values you need to set:

- `environment_name`
- `location`
- `app_service_plan_id`
- `acr_id`
- `acr_login_server`
- `study_service_base_url`
- one of:
  - `client_registry_connection_string_secret_uri`
  - `client_registry_connection_string`
- if Redis is enabled, one of:
  - `redis_connection_string_secret_uri`
  - `redis_connection_string`

Strong recommendation for the first deployment:

- set `bootstrap_container_image_tag` to a tag that already exists in ACR
- the safest initial value is `latest`, because the pipeline pushes `latest`

Example `terraformVariablesJson` for `dev`:

```json
{
  "environment_name": "dev",
  "location": "australiaeast",
  "global_name_suffix": "01",
  "app_service_plan_id": "/subscriptions/<subscription-id>/resourceGroups/<shared-rg>/providers/Microsoft.Web/serverfarms/<shared-plan>",
  "acr_id": "/subscriptions/<subscription-id>/resourceGroups/<shared-rg>/providers/Microsoft.ContainerRegistry/registries/<shared-acr>",
  "acr_login_server": "<shared-acr>.azurecr.io",
  "key_vault_id": "/subscriptions/<subscription-id>/resourceGroups/<shared-rg>/providers/Microsoft.KeyVault/vaults/<shared-kv>",
  "key_vault_uri": "https://<shared-kv>.vault.azure.net/",
  "log_analytics_workspace_id": "/subscriptions/<subscription-id>/resourceGroups/<shared-rg>/providers/Microsoft.OperationalInsights/workspaces/<shared-law>",
  "sql_server_id": "/subscriptions/<subscription-id>/resourceGroups/<shared-rg>/providers/Microsoft.Sql/servers/<shared-sql-server>",
  "create_client_registry_database": true,
  "client_registry_database_name": "sqldb-airydocs-mcp-dev",
  "create_application_insights": true,
  "study_service_base_url": "https://<study-service-dev-host>/",
  "study_service_lookup_path_template": "api/studies/{nctId}",
  "enable_redis": true,
  "client_registry_connection_string_secret_uri": "https://<shared-kv>.vault.azure.net/secrets/airydocs-mcp-client-registry-connection-string/<version>",
  "redis_connection_string_secret_uri": "https://<shared-kv>.vault.azure.net/secrets/airydocs-mcp-redis-connection-string/<version>",
  "container_image_repository": "airydocs/mcp",
  "bootstrap_container_image_tag": "latest",
  "enable_staging_slot": true,
  "staging_slot_name": "staging",
  "enable_container_apps_validation": false
}
```

## Step 5: Create Azure DevOps Service Connections

The checked-in pipeline expects two service connections:

- `azureServiceConnection`
- `dockerRegistryServiceConnection`

Create:

1. An Azure Resource Manager service connection with permission to:
   - read shared resources
   - create or update the MCP resource group
   - deploy App Service, slot, identity, database, and optional monitoring resources
   - read or reference Key Vault where required

2. A Docker Registry service connection to the shared ACR.

Recommended naming pattern:

- `sc-airydocs-mcp-azure-dev`
- `sc-airydocs-mcp-acr-dev`
- `sc-airydocs-mcp-azure-staging`
- `sc-airydocs-mcp-acr-staging`
- `sc-airydocs-mcp-azure-prod`
- `sc-airydocs-mcp-acr-prod`

The exact names are your choice. The important part is that the names in Azure DevOps match the values in the variable groups.

## Step 6: Create Azure DevOps Variable Groups

The pipeline imports a variable group by environment name:

- `airydocs-mcp-dev`
- `airydocs-mcp-staging`
- `airydocs-mcp-prod`

Create one variable group per environment with these exact names.

Each group should contain:

- `azureServiceConnection`
  - name of the Azure Resource Manager service connection
- `dockerRegistryServiceConnection`
  - name of the Docker Registry or ACR service connection
- `terraformStateResourceGroupName`
  - resource group that holds the Terraform backend storage account
- `terraformStateStorageAccountName`
  - Terraform backend storage account name
- `terraformStateContainerName`
  - Terraform backend blob container name
- `terraformVariablesJson`
  - the full Terraform input object as JSON

Notes:

- `terraformVariablesJson` can be multi-line JSON
- if it contains raw secrets, mark it as secret
- better practice is to keep raw secrets out of it and use Key Vault secret URIs instead

## Step 7: Create the Azure DevOps Pipeline

Use the checked-in YAML file:

- [azure-pipelines.yml](/e:/GitHimal/ClinicalTrialMCPDemo/azure-pipelines.yml)

In Azure DevOps:

1. Go to `Pipelines`.
2. Select `New pipeline`.
3. Choose the repo that contains this code.
4. Choose `Existing Azure Pipelines YAML file`.
5. Select `azure-pipelines.yml`.
6. Save the pipeline.
7. Authorize the variable groups and service connections when prompted.

Important current behavior:

- the checked-in YAML triggers on `main`
- it also triggers on pull requests

If you do not want deployment-capable pipeline runs on PRs or every merge to `main`, change the triggers before enabling it broadly.

## Step 8: Queue the First Deployment

Run the first deployment manually for `dev`.

Pipeline parameters:

- `environment`
  - allowed values: `dev`, `staging`, `prod`
- `deployContainerAppsValidation`
  - `false` for the normal path
  - `true` only when you want the optional non-prod Container Apps compatibility deployment updated too

Recommended first run:

- `environment = dev`
- `deployContainerAppsValidation = false`

What the pipeline does:

1. restore, build, and test the .NET solution
2. build and push the MCP container image to ACR
3. write `infra/terraform.auto.tfvars.json`
4. run `terraform init`
5. run `terraform validate`
6. run `terraform plan`
7. run `terraform apply`
8. deploy the built image to the staging slot
9. smoke test `/health/ready`
10. swap the staging slot into production

## Step 9: Apply the SQL Schema

After the infrastructure exists, apply the client-registry schema.

Schema script:

- [001_create_client_registry.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/001_create_client_registry.sql)

Do not run the local-only seed script in shared environments:

- [002_seed_local_dev_client.sql](/e:/GitHimal/ClinicalTrialMCPDemo/SQL/002_seed_local_dev_client.sql)

Current concern:

- the checked-in schema script hardcodes the database name `ClinicalTrialsMcp`
- the Terraform examples use Azure SQL database names like `sqldb-airydocs-mcp-dev`

Because of that, choose one path before your first Azure deployment:

1. Keep the schema script authoritative and set `create_client_registry_database = false`, then use the script to create the database and tables.
2. Let Terraform create the database, then adapt the first `CREATE DATABASE` and `USE` section of the script so it targets the Terraform-created database.

Until that naming mismatch is reconciled, do not assume the checked-in script can be run unchanged against a Terraform-created `sqldb-airydocs-mcp-<env>` database.

## Step 10: Bootstrap the First API Client

After the schema exists, create at least one client and issue one key.

Admin tool:

- [Program.cs](/e:/GitHimal/ClinicalTrialMCPDemo/ClinicalTrials.Admin/ClinicalTrials.Admin/Program.cs)

Create the client:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- create-client --connection-string "<client-registry-connection-string>" --client-code "<client-code>" --display-name "<display-name>"
```

Issue the key:

```powershell
dotnet run --project .\ClinicalTrials.Admin\ClinicalTrials.Admin\ClinicalTrials.Admin.csproj -- issue-key --connection-string "<client-registry-connection-string>" --client-code "<client-code>" --key-label primary
```

Capture the raw key immediately. It is only shown once.

## Step 11: Verify the Deployment

After the pipeline run completes and the schema exists:

1. Check the health endpoints:
   - `https://<web-app-host>/health/live`
   - `https://<web-app-host>/health/ready`
2. Confirm the App Service configuration includes:
   - `StudyService__BaseUrl`
   - `ConnectionStrings__ClientRegistry`
   - `ConnectionStrings__Redis` when Redis is enabled
3. Confirm Key Vault references resolve successfully if used.
4. Use an MCP-capable client with:
   - endpoint: `https://<web-app-host>/mcp`
   - header: `X-Api-Key`
5. Call `get_study_by_nct_id` with a known NCT ID.

## Step 12: Promote to Staging and Production

Repeat the same pattern for `staging`, then `prod`:

1. create the environment variable group
2. queue the pipeline with the matching `environment` parameter
3. apply the schema to the target client-registry database
4. bootstrap or rotate the target environment client keys
5. verify health and real MCP calls

Do not reuse `dev` API keys in higher environments.

## Optional Manual Terraform Path

If you want to deploy manually before wiring Azure DevOps, use:

```powershell
terraform -chdir=infra init -backend-config="environments/dev/backend.hcl"
terraform -chdir=infra plan -var-file="environments/dev/dev.auto.tfvars"
terraform -chdir=infra apply -var-file="environments/dev/dev.auto.tfvars"
```

Then inspect outputs:

```powershell
terraform -chdir=infra output web_app_default_hostname
terraform -chdir=infra output staging_slot_default_hostname
```

The pipeline remains the preferred path because it couples infrastructure rollout, image build, slot deployment, smoke test, and slot swap in one flow.

## Practical Concerns

- The current pipeline is deployment-capable on `main` and PR events. Review that before broad enablement.
- The first deployment must use a `bootstrap_container_image_tag` that already exists in ACR. `latest` is the safest initial choice because the pipeline pushes it.
- The current SQL schema script hardcodes `ClinicalTrialsMcp`. Decide how to handle that before the first Azure rollout.
- The MCP host expects the upstream study service to be reachable from App Service. Validate DNS, public ingress, and auth between the MCP host and that service before production cutover.
