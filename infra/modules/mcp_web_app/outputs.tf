output "web_app_name" {
  value = azurerm_linux_web_app.mcp.name
}

output "web_app_id" {
  value = azurerm_linux_web_app.mcp.id
}

output "web_app_default_hostname" {
  value = azurerm_linux_web_app.mcp.default_hostname
}

output "staging_slot_name" {
  value = try(azurerm_linux_web_app_slot.staging[0].name, null)
}

output "staging_slot_id" {
  value = try(azurerm_linux_web_app_slot.staging[0].id, null)
}

output "staging_slot_default_hostname" {
  value = try(azurerm_linux_web_app_slot.staging[0].default_hostname, null)
}

output "managed_identity_id" {
  value = azurerm_user_assigned_identity.mcp.id
}

output "managed_identity_client_id" {
  value = azurerm_user_assigned_identity.mcp.client_id
}

output "managed_identity_principal_id" {
  value = azurerm_user_assigned_identity.mcp.principal_id
}
