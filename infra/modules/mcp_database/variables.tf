variable "create_database" {
  type = bool
}

variable "database_name" {
  type = string
}

variable "sql_server_id" {
  type    = string
  default = null
}

variable "sku_name" {
  type    = string
  default = "Basic"
}

variable "max_size_gb" {
  type    = number
  default = 2
}

variable "collation" {
  type    = string
  default = "SQL_Latin1_General_CP1_CI_AS"
}

variable "zone_redundant" {
  type    = bool
  default = false
}

variable "tags" {
  type    = map(string)
  default = {}
}
