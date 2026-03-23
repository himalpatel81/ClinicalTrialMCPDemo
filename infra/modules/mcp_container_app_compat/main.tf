locals {
  container_image_name = "${var.acr_login_server}/${var.container_image_repository}:${var.bootstrap_container_image_tag}"
  secret_name_map = {
    for key, value in var.secret_key_vault_secret_ids :
    key => replace(replace(lower(key), "__", "-"), "_", "-")
  }
}

resource "azurerm_user_assigned_identity" "compat" {
  count               = var.enabled ? 1 : 0
  name                = var.managed_identity_name
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  count                = var.enabled ? 1 : 0
  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.compat[0].principal_id
}

resource "azurerm_role_assignment" "key_vault_secrets_user" {
  count                = var.enabled && var.key_vault_id != null ? 1 : 0
  scope                = var.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.compat[0].principal_id
}

resource "azurerm_container_app_environment" "compat" {
  count                      = var.enabled ? 1 : 0
  name                       = var.container_app_environment_name
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = var.log_analytics_workspace_id
  tags                       = var.tags
}

resource "azurerm_container_app" "compat" {
  count                        = var.enabled ? 1 : 0
  name                         = var.container_app_name
  resource_group_name          = var.resource_group_name
  container_app_environment_id = azurerm_container_app_environment.compat[0].id
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.compat[0].id]
  }

  registry {
    server   = var.acr_login_server
    identity = azurerm_user_assigned_identity.compat[0].id
  }

  dynamic "secret" {
    for_each = var.secret_key_vault_secret_ids
    content {
      name                = local.secret_name_map[secret.key]
      identity            = azurerm_user_assigned_identity.compat[0].id
      key_vault_secret_id = secret.value
    }
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = "airydocs-mcp"
      image  = local.container_image_name
      cpu    = 0.5
      memory = "1Gi"

      dynamic "env" {
        for_each = var.environment_variables
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.secret_key_vault_secret_ids
        content {
          name        = env.key
          secret_name = local.secret_name_map[env.key]
        }
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = var.container_port

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}
