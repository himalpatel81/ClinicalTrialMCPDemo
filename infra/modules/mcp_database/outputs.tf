output "database_name" {
  value = try(azurerm_mssql_database.client_registry[0].name, null)
}

output "database_id" {
  value = try(azurerm_mssql_database.client_registry[0].id, null)
}
