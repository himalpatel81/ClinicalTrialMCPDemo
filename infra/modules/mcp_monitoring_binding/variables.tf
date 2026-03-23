variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "application_insights_name" {
  type = string
}

variable "create_application_insights" {
  type = bool
}

variable "existing_application_insights_connection_string" {
  type      = string
  default   = null
  sensitive = true
}

variable "log_analytics_workspace_id" {
  type    = string
  default = null
}

variable "tags" {
  type    = map(string)
  default = {}
}
