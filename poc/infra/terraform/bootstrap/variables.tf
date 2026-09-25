variable "subscription_id" {
  description = "Azure subscription ID for provisioning."
  type        = string
}

variable "location" {
  description = "Azure region for Terraform state resources."
  type        = string
  default     = "westus2"
}

variable "resource_group_name" {
  description = "Resource group name that stores Terraform state resources."
  type        = string
  default     = "courtflow-tfstate-rg"
}

variable "storage_account_name_prefix" {
  description = "Storage account prefix (3-18 lowercase letters/numbers). Random suffix will be appended."
  type        = string
  default     = "courtflowtf"

  validation {
    condition     = can(regex("^[a-z0-9]{3,18}$", var.storage_account_name_prefix))
    error_message = "storage_account_name_prefix must be 3-18 lowercase letters or digits."
  }
}

variable "container_name" {
  description = "Blob container name for Terraform state."
  type        = string
  default     = "tfstate"
}
