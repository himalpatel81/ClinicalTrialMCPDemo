locals {
  normalized_suffix = (
    var.global_name_suffix == null || trimspace(var.global_name_suffix) == ""
  ) ? "" : "-${lower(trimspace(var.global_name_suffix))}"

  common_tags = merge(
    {
      application = "airydocs-mcp"
      environment = var.environment_name
      managed-by  = "terraform"
    },
  var.tags)

  resource_group_name           = coalesce(var.resource_group_name_override, "rg-airydocs-mcp-${var.environment_name}")
  web_app_name                  = substr("app-airydocs-mcp-${var.environment_name}${local.normalized_suffix}", 0, 60)
  managed_identity_name         = substr("id-airydocs-mcp-${var.environment_name}", 0, 64)
  application_insights_name     = substr("appi-airydocs-mcp-${var.environment_name}", 0, 64)
  client_registry_database_name = coalesce(var.client_registry_database_name, "sqldb-airydocs-mcp-${var.environment_name}")
  container_app_environment_name = substr(
    "cae-airydocs-mcp-${var.environment_name}",
    0,
  60)
  container_app_name = substr(
    "ca-airydocs-mcp-${var.environment_name}${local.normalized_suffix}",
    0,
  32)

  telemetry_connection_string = coalesce(
    module.monitoring_binding.application_insights_connection_string,
    var.existing_application_insights_connection_string,
    var.telemetry_connection_string_override,
  "")

  secrets_provider                 = var.key_vault_uri != null ? "AzureKeyVault" : "Local"
  cache_provider                   = var.enable_redis ? "Redis" : "Memory"
  rate_limit_provider              = var.enable_redis ? "Redis" : "Memory"
  client_registry_connection_value = var.client_registry_connection_string_secret_uri != null ? "@Microsoft.KeyVault(SecretUri=${var.client_registry_connection_string_secret_uri})" : coalesce(var.client_registry_connection_string, "")
  redis_connection_value = !var.enable_redis ? "" : (
    var.redis_connection_string_secret_uri != null ? "@Microsoft.KeyVault(SecretUri=${var.redis_connection_string_secret_uri})" : coalesce(var.redis_connection_string, "")
  )

  base_app_settings = merge(
    {
      ASPNETCORE_ENVIRONMENT                = "Production"
      Hosting__ForwardedHeaders__Enabled    = "true"
      AllowedHosts                          = var.allowed_hosts
      StudyService__BaseUrl                 = var.study_service_base_url
      StudyService__StudyLookupPathTemplate = var.study_service_lookup_path_template
      Caching__Provider                     = local.cache_provider
      RateLimiting__Provider                = local.rate_limit_provider
      Secrets__Provider                     = local.secrets_provider
      Telemetry__Console__Enabled           = tostring(var.telemetry_console_enabled)
      WEBSITE_HEALTHCHECK_MAXPINGFAILURES   = "10"
    },
    var.key_vault_uri != null ? {
      Secrets__AzureKeyVault__VaultUri = var.key_vault_uri
    } : {},
    length(local.client_registry_connection_value) > 0 ? {
      ConnectionStrings__ClientRegistry = local.client_registry_connection_value
    } : {},
    var.enable_redis && length(local.redis_connection_value) > 0 ? {
      ConnectionStrings__Redis = local.redis_connection_value
    } : {},
    length(local.telemetry_connection_string) > 0 ? {
      Telemetry__ApplicationInsights__ConnectionString = local.telemetry_connection_string
    } : {},
  var.app_settings_overrides)

  slot_app_settings = merge(
    {
      WEBSITE_SWAP_WARMUP_PING_PATH     = "/health/ready"
      WEBSITE_SWAP_WARMUP_PING_STATUSES = "200-399"
    },
  var.slot_app_settings_overrides)

  container_apps_environment_variables = merge(
    {
      ASPNETCORE_ENVIRONMENT                = "Production"
      ASPNETCORE_URLS                       = "http://+:${var.container_port}"
      AllowedHosts                          = "*"
      Hosting__ForwardedHeaders__Enabled    = "true"
      StudyService__BaseUrl                 = var.study_service_base_url
      StudyService__StudyLookupPathTemplate = var.study_service_lookup_path_template
      Caching__Provider                     = local.cache_provider
      RateLimiting__Provider                = local.rate_limit_provider
      Secrets__Provider                     = local.secrets_provider
      Telemetry__Console__Enabled           = tostring(var.telemetry_console_enabled)
    },
    var.key_vault_uri != null ? {
      Secrets__AzureKeyVault__VaultUri = var.key_vault_uri
    } : {},
    length(local.telemetry_connection_string) > 0 ? {
      Telemetry__ApplicationInsights__ConnectionString = local.telemetry_connection_string
    } : {},
    var.client_registry_connection_string_secret_uri == null && var.client_registry_connection_string != null ? {
      ConnectionStrings__ClientRegistry = var.client_registry_connection_string
    } : {},
    var.enable_redis && var.redis_connection_string_secret_uri == null && var.redis_connection_string != null ? {
      ConnectionStrings__Redis = var.redis_connection_string
    } : {},
  var.container_apps_plain_environment_overrides)

  container_apps_secret_key_vault_secret_ids = merge(
    var.client_registry_connection_string_secret_uri != null ? {
      ConnectionStrings__ClientRegistry = var.client_registry_connection_string_secret_uri
    } : {},
    var.enable_redis && var.redis_connection_string_secret_uri != null ? {
      ConnectionStrings__Redis = var.redis_connection_string_secret_uri
    } : {},
  var.container_apps_additional_secret_key_vault_secret_ids)
}

check "client_registry_connection_string_supplied" {
  assert {
    condition     = var.client_registry_connection_string_secret_uri != null || var.client_registry_connection_string != null
    error_message = "Provide either client_registry_connection_string_secret_uri or client_registry_connection_string."
  }
}

check "redis_connection_string_supplied_when_enabled" {
  assert {
    condition     = !var.enable_redis || var.redis_connection_string_secret_uri != null || var.redis_connection_string != null
    error_message = "Provide either redis_connection_string_secret_uri or redis_connection_string when enable_redis is true."
  }
}

check "key_vault_configuration_supplied_when_secret_references_are_used" {
  assert {
    condition = !(
      var.client_registry_connection_string_secret_uri != null
      || var.redis_connection_string_secret_uri != null
      || length(var.container_apps_additional_secret_key_vault_secret_ids) > 0
    ) || (var.key_vault_id != null && var.key_vault_uri != null)
    error_message = "key_vault_id and key_vault_uri are required when any Key Vault secret reference is used."
  }
}

check "sql_server_supplied_when_database_creation_is_enabled" {
  assert {
    condition     = !var.create_client_registry_database || var.sql_server_id != null
    error_message = "sql_server_id is required when create_client_registry_database is true."
  }
}

check "log_analytics_workspace_supplied_when_container_apps_validation_is_enabled" {
  assert {
    condition = !var.enable_container_apps_validation || coalesce(
      var.container_apps_log_analytics_workspace_id,
      var.log_analytics_workspace_id,
    "") != ""
    error_message = "container_apps_log_analytics_workspace_id or log_analytics_workspace_id is required when enable_container_apps_validation is true."
  }
}

resource "azurerm_resource_group" "mcp" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.common_tags
}

module "monitoring_binding" {
  source = "./modules/mcp_monitoring_binding"

  resource_group_name                             = azurerm_resource_group.mcp.name
  location                                        = azurerm_resource_group.mcp.location
  application_insights_name                       = local.application_insights_name
  create_application_insights                     = var.create_application_insights
  existing_application_insights_connection_string = var.existing_application_insights_connection_string
  log_analytics_workspace_id                      = var.log_analytics_workspace_id
  tags                                            = local.common_tags
}

module "client_registry_database" {
  source = "./modules/mcp_database"

  create_database = var.create_client_registry_database
  database_name   = local.client_registry_database_name
  sql_server_id   = var.sql_server_id
  sku_name        = var.client_registry_database_sku_name
  max_size_gb     = var.client_registry_database_max_size_gb
  collation       = var.client_registry_database_collation
  zone_redundant  = var.client_registry_database_zone_redundant
  tags            = local.common_tags
}

module "web_app" {
  source = "./modules/mcp_web_app"

  resource_group_name           = azurerm_resource_group.mcp.name
  location                      = azurerm_resource_group.mcp.location
  web_app_name                  = local.web_app_name
  slot_name                     = var.staging_slot_name
  service_plan_id               = var.app_service_plan_id
  acr_id                        = var.acr_id
  acr_login_server              = var.acr_login_server
  managed_identity_name         = local.managed_identity_name
  key_vault_id                  = var.key_vault_id
  container_image_repository    = var.container_image_repository
  bootstrap_container_image_tag = var.bootstrap_container_image_tag
  container_port                = var.container_port
  enable_staging_slot           = var.enable_staging_slot
  public_network_access_enabled = var.public_network_access_enabled
  app_settings                  = local.base_app_settings
  slot_app_settings             = local.slot_app_settings
  tags                          = local.common_tags
}

module "container_app_compat" {
  source = "./modules/mcp_container_app_compat"

  enabled                        = var.enable_container_apps_validation
  resource_group_name            = azurerm_resource_group.mcp.name
  location                       = azurerm_resource_group.mcp.location
  container_app_environment_name = local.container_app_environment_name
  container_app_name             = local.container_app_name
  managed_identity_name          = "${local.managed_identity_name}-ca"
  log_analytics_workspace_id     = coalesce(var.container_apps_log_analytics_workspace_id, var.log_analytics_workspace_id, "")
  acr_id                         = var.acr_id
  acr_login_server               = var.acr_login_server
  key_vault_id                   = var.key_vault_id
  container_image_repository     = var.container_image_repository
  bootstrap_container_image_tag  = var.bootstrap_container_image_tag
  container_port                 = var.container_port
  environment_variables          = local.container_apps_environment_variables
  secret_key_vault_secret_ids    = local.container_apps_secret_key_vault_secret_ids
  min_replicas                   = var.container_apps_min_replicas
  max_replicas                   = var.container_apps_max_replicas
  tags                           = local.common_tags
}
