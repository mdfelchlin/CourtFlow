output "id" {
  value       = azurerm_container_app.this.id
  description = "Container App ID."
}

output "name" {
  value       = azurerm_container_app.this.name
  description = "Container App name."
}

output "latest_revision_fqdn" {
  value       = azurerm_container_app.this.latest_revision_fqdn
  description = "Container App latest revision FQDN."
}
