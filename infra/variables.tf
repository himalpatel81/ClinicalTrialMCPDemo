variable "environment_name" {
  type = string
}

variable "location" {
  type = string
}

variable "global_name_suffix" {
  type    = string
  default = null
}

variable "resource_group_name_override" {
  type    = string
  default = null
}

variable "tags" {
  type    = map(string)
  default = {}
}

variable "app_service_plan_id" {
  type = string
}

variable "acr_id" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "key_vault_id" {
  type    = string
  default = null
}

variable "key_vault_uri" {
  type    = string
  default = null
}

variable "log_analytics_workspace_id" {
  type    = string
  default = null
}

variable "sql_server_id" {
  type    = string
  default = null
}

variable "create_client_registry_database" {
  type    = bool
  default = false
}

variable "client_registry_database_name" {
  type    = string
  default = null
}

variable "client_registry_database_sku_name" {
  type    = string
  default = "Basic"
}

variable "client_registry_database_max_size_gb" {
  type    = number
  default = 2
}

variable "client_registry_database_collation" {
  type    = string
  default = "SQL_Latin1_General_CP1_CI_AS"
}

variable "client_registry_database_zone_redundant" {
  type    = bool
  default = false
}

variable "create_application_insights" {
  type    = bool
  default = false
}

variable "existing_application_insights_connection_string" {
  type      = string
  default   = null
  sensitive = true
}

variable "telemetry_connection_string_override" {
  type      = string
  default   = null
  sensitive = true
}

variable "study_service_base_url" {
  type = string
}

variable "study_service_lookup_path_template" {
  type    = string
  default = "api/studies/{nctId}"
}

variable "allowed_hosts" {
  type    = string
  default = "*"
}

variable "container_image_repository" {
  type    = string
  default = "airydocs/mcp"
}

variable "bootstrap_container_image_tag" {
  type    = string
  default = "stable"
}

variable "container_port" {
  type    = number
  default = 8080
}

variable "enable_redis" {
  type    = bool
  default = true
}

variable "client_registry_connection_string_secret_uri" {
  type    = string
  default = null
}

variable "client_registry_connection_string" {
  type      = string
  default   = null
  sensitive = true
}

variable "redis_connection_string_secret_uri" {
  type    = string
  default = null
}

variable "redis_connection_string" {
  type      = string
  default   = null
  sensitive = true
}

variable "telemetry_console_enabled" {
  type    = bool
  default = false
}

variable "enable_staging_slot" {
  type    = bool
  default = true
}

variable "staging_slot_name" {
  type    = string
  default = "staging"
}

variable "public_network_access_enabled" {
  type    = bool
  default = true
}

variable "app_settings_overrides" {
  type    = map(string)
  default = {}
}

variable "slot_app_settings_overrides" {
  type    = map(string)
  default = {}
}

variable "enable_container_apps_validation" {
  type    = bool
  default = false
}

variable "container_apps_log_analytics_workspace_id" {
  type    = string
  default = null
}

variable "container_apps_plain_environment_overrides" {
  type    = map(string)
  default = {}
}

variable "container_apps_additional_secret_key_vault_secret_ids" {
  type    = map(string)
  default = {}
}

variable "container_apps_min_replicas" {
  type    = number
  default = 1
}

variable "container_apps_max_replicas" {
  type    = number
  default = 1
}
