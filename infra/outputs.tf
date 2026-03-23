output "resource_group_name" {
  value = azurerm_resource_group.mcp.name
}

output "web_app_name" {
  value = module.web_app.web_app_name
}

output "web_app_default_hostname" {
  value = module.web_app.web_app_default_hostname
}

output "staging_slot_name" {
  value = coalesce(module.web_app.staging_slot_name, "")
}

output "staging_slot_default_hostname" {
  value = coalesce(module.web_app.staging_slot_default_hostname, "")
}

output "managed_identity_id" {
  value = module.web_app.managed_identity_id
}

output "managed_identity_client_id" {
  value = module.web_app.managed_identity_client_id
}

output "managed_identity_principal_id" {
  value = module.web_app.managed_identity_principal_id
}

output "application_insights_connection_string" {
  value     = module.monitoring_binding.application_insights_connection_string
  sensitive = true
}

output "client_registry_database_name" {
  value = coalesce(module.client_registry_database.database_name, "")
}

output "compatibility_container_app_name" {
  value = coalesce(module.container_app_compat.container_app_name, "")
}
