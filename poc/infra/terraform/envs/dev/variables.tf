variable "subscription_id" {
  description = "Azure subscription ID."
  type        = string
}

variable "location" {
  description = "Azure region."
  type        = string
  default     = "westus2"
}

variable "project_name" {
  description = "Project prefix used for resource naming."
  type        = string
  default     = "courtflow"
}

variable "environment" {
  description = "Environment name."
  type        = string
  default     = "dev"
}

variable "max_replicas" {
  description = "Maximum API replicas."
  type        = number
  default     = 3
}

variable "min_replicas" {
  description = "Minimum API replicas."
  type        = number
  default     = 0
}

variable "api_image" {
  description = "Bootstrap API image."
  type        = string
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "cors_origins" {
  description = "Allowed CORS origins for API."
  type        = list(string)
  default     = []
}

variable "budget_amount" {
  description = "Monthly budget amount."
  type        = number
  default     = 25
}

variable "budget_start_date" {
  description = "Optional budget start (RFC3339, first of month is typical). If null, use first day of the current month at plan time."
  type        = string
  default     = null
  nullable    = true
}

variable "budget_end_date" {
  description = "Budget end date RFC3339."
  type        = string
  default     = "2030-01-01T00:00:00Z"
}

variable "budget_contact_emails" {
  description = "Emails for budget alerts."
  type        = list(string)
  default     = []
}

variable "secrets" {
  description = "Container App secrets map. Keep values outside source control."
  type        = map(string)
  sensitive   = true
  default     = {}
}

variable "connection_string_default" {
  description = "Connection string for Turso/libSQL."
  type        = string
  default     = ""
  sensitive   = true
}

variable "clerk_authority" {
  description = "Clerk authority (issuer)."
  type        = string
  default     = ""
}

variable "clerk_audience" {
  description = "Clerk audience."
  type        = string
  default     = ""
}
