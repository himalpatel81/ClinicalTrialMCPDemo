output "container_app_name" {
  value = try(azurerm_container_app.compat[0].name, null)
}

output "container_app_id" {
  value = try(azurerm_container_app.compat[0].id, null)
}

output "container_app_environment_id" {
  value = try(azurerm_container_app_environment.compat[0].id, null)
}
