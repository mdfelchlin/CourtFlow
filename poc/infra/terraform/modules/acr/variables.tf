variable "name" {
  type        = string
  description = "Azure Container Registry name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group where ACR is created."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "sku" {
  type        = string
  description = "ACR SKU."
  default     = "Basic"
}

variable "admin_enabled" {
  type        = bool
  description = "Enable ACR admin user."
  default     = false
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
