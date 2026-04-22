variable "name" {
  type        = string
  description = "Static Web App name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group where SWA is created."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "sku_tier" {
  type        = string
  description = "SWA SKU tier."
  default     = "Free"
}

variable "sku_size" {
  type        = string
  description = "SWA SKU size."
  default     = "Free"
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
