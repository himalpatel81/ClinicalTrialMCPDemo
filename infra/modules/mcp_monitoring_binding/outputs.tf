output "application_insights_connection_string" {
  value     = var.create_application_insights ? azurerm_application_insights.mcp[0].connection_string : var.existing_application_insights_connection_string
  sensitive = true
}

output "application_insights_id" {
  value = try(azurerm_application_insights.mcp[0].id, null)
}
