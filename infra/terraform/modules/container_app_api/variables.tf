variable "name" {
  type        = string
  description = "Container App name."
}

variable "resource_group_name" {
  type        = string
  description = "Resource group where app is created."
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "container_app_environment_id" {
  type        = string
  description = "Container Apps environment ID."
}

variable "image" {
  type        = string
  description = "Bootstrap image reference."
  default     = "mcr.microsoft.com/k8se/quickstart:latest"
}

variable "target_port" {
  type        = number
  description = "Ingress target port."
  default     = 8080
}

variable "ingress_external_enabled" {
  type        = bool
  description = "Enable external ingress."
  default     = true
}

variable "min_replicas" {
  type        = number
  description = "Minimum replica count."
  default     = 0
}

variable "max_replicas" {
  type        = number
  description = "Maximum replica count."
  default     = 3
}

variable "cpu" {
  type        = number
  description = "CPU cores for API container."
  default     = 0.5
}

variable "memory" {
  type        = string
  description = "Memory for API container."
  default     = "1Gi"
}

variable "env_vars" {
  type        = map(string)
  description = "Non-secret environment variables."
  default     = {}
}

variable "secret_env_vars" {
  type        = map(string)
  description = "Map of env var name to secret name."
  default     = {}
}

variable "secrets" {
  type        = map(string)
  description = "Container App secrets map."
  default     = {}
  sensitive   = true
}

variable "use_acr_integration" {
  type        = bool
  description = "If true, create a managed identity and AcrPull; configure registry. Must be a literal bool (not derived from ACR id) so Terraform can plan count at plan time when ACR is created in the same apply."
  default     = true
}

variable "acr_id" {
  type        = string
  description = "ACR resource ID for AcrPull (required when use_acr_integration is true)."
  default     = null
  nullable    = true
}

variable "acr_login_server" {
  type        = string
  description = "ACR login server for container registry (required when use_acr_integration is true)."
  default     = null
  nullable    = true
}

variable "tags" {
  type        = map(string)
  description = "Resource tags."
  default     = {}
}
