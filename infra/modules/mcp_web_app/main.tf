locals {
  container_image_name = "${var.container_image_repository}:${var.bootstrap_container_image_tag}"
  base_app_settings = {
    ASPNETCORE_URLS                   = "http://+:${var.container_port}"
    WEBSITES_PORT                     = tostring(var.container_port)
    WEBSITE_HEALTHCHECK_PATH          = var.health_check_path
    WEBSITE_SWAP_WARMUP_PING_PATH     = var.health_check_path
    WEBSITE_SWAP_WARMUP_PING_STATUSES = "200-399"
  }
}

resource "azurerm_user_assigned_identity" "mcp" {
  name                = var.managed_identity_name
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.mcp.principal_id
}

resource "azurerm_role_assignment" "key_vault_secrets_user" {
  count                = var.key_vault_id != null ? 1 : 0
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.mcp.principal_id
}

resource "azurerm_linux_web_app" "mcp" {
  name                            = var.web_app_name
  location                        = var.location
  resource_group_name             = var.resource_group_name
  service_plan_id                 = var.service_plan_id
  https_only                      = true
  public_network_access_enabled   = var.public_network_access_enabled
  key_vault_reference_identity_id = var.key_vault_id != null ? azurerm_user_assigned_identity.mcp.id : null
  tags                            = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.mcp.id]
  }

  site_config {
    always_on                                     = true
    http2_enabled                                 = true
    ftps_state                                    = "Disabled"
    minimum_tls_version                           = "1.2"
    health_check_path                             = var.health_check_path
    health_check_eviction_time_in_min             = 5
    container_registry_use_managed_identity       = true
    container_registry_managed_identity_client_id = azurerm_user_assigned_identity.mcp.client_id

    application_stack {
      docker_image_name   = local.container_image_name
      docker_registry_url = "https://${var.acr_login_server}"
    }
  }

  app_settings = merge(local.base_app_settings, var.app_settings)
}

resource "azurerm_linux_web_app_slot" "staging" {
  count                           = var.enable_staging_slot ? 1 : 0
  name                            = var.slot_name
  app_service_id                  = azurerm_linux_web_app.mcp.id
  https_only                      = true
  key_vault_reference_identity_id = var.key_vault_id != null ? azurerm_user_assigned_identity.mcp.id : null
  tags                            = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.mcp.id]
  }

  site_config {
    always_on                                     = true
    http2_enabled                                 = true
    ftps_state                                    = "Disabled"
    minimum_tls_version                           = "1.2"
    health_check_path                             = var.health_check_path
    health_check_eviction_time_in_min             = 5
    container_registry_use_managed_identity       = true
    container_registry_managed_identity_client_id = azurerm_user_assigned_identity.mcp.client_id

    application_stack {
      docker_image_name   = local.container_image_name
      docker_registry_url = "https://${var.acr_login_server}"
    }
  }

  app_settings = merge(local.base_app_settings, var.app_settings, var.slot_app_settings)
}
