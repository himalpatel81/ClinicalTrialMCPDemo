resource "azurerm_mssql_database" "client_registry" {
  count          = var.create_database ? 1 : 0
  name           = var.database_name
  server_id      = var.sql_server_id
  sku_name       = var.sku_name
  max_size_gb    = var.max_size_gb
  collation      = var.collation
  zone_redundant = var.zone_redundant
  tags           = var.tags
}
