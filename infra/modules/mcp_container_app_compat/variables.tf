variable "enabled" {
  type = bool
}

variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "container_app_environment_name" {
  type = string
}

variable "container_app_name" {
  type = string
}

variable "managed_identity_name" {
  type = string
}

variable "log_analytics_workspace_id" {
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

variable "container_image_repository" {
  type = string
}

variable "bootstrap_container_image_tag" {
  type = string
}

variable "container_port" {
  type    = number
  default = 8080
}

variable "environment_variables" {
  type    = map(string)
  default = {}
}

variable "secret_key_vault_secret_ids" {
  type    = map(string)
  default = {}
}

variable "min_replicas" {
  type    = number
  default = 1
}

variable "max_replicas" {
  type    = number
  default = 1
}

variable "tags" {
  type    = map(string)
  default = {}
}
