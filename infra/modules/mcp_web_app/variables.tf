variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "web_app_name" {
  type = string
}

variable "slot_name" {
  type    = string
  default = "staging"
}

variable "service_plan_id" {
  type = string
}

variable "acr_id" {
  type = string
}

variable "acr_login_server" {
  type = string
}

variable "managed_identity_name" {
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

variable "health_check_path" {
  type    = string
  default = "/health/ready"
}

variable "enable_staging_slot" {
  type    = bool
  default = true
}

variable "public_network_access_enabled" {
  type    = bool
  default = true
}

variable "app_settings" {
  type    = map(string)
  default = {}
}

variable "slot_app_settings" {
  type    = map(string)
  default = {}
}

variable "tags" {
  type    = map(string)
  default = {}
}
