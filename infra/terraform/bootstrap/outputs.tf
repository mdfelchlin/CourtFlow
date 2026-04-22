output "backend_resource_group_name" {
  description = "Resource group containing Terraform state storage."
  value       = azurerm_resource_group.state.name
}

output "backend_storage_account_name" {
  description = "Storage account used for Terraform remote state."
  value       = azurerm_storage_account.state.name
}

output "backend_container_name" {
  description = "Blob container used for Terraform state."
  value       = azurerm_storage_container.state.name
}

output "backend_key_dev" {
  description = "Recommended blob key for dev environment."
  value       = "dev.terraform.tfstate"
}

output "backend_key_prod" {
  description = "Recommended blob key for prod environment."
  value       = "prod.terraform.tfstate"
}
