variable "name" {
  type        = string
  description = "Log Analytics workspace name."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group where workspace is created."
}

variable "sku" {
  type        = string
  description = "Workspace SKU."
  default     = "PerGB2018"
}

variable "retention_in_days" {
  type        = number
  description = "Log retention days."
  default     = 30
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
