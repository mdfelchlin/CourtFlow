variable "name" {
  type        = string
  description = "Container Apps environment name."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group where environment is created."
}

variable "log_analytics_workspace_id" {
  type        = string
  description = "Log Analytics workspace resource ID."
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
