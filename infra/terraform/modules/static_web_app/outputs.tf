output "id" {
  value       = azurerm_static_web_app.this.id
  description = "Static Web App ID."
}

output "name" {
  value       = azurerm_static_web_app.this.name
  description = "Static Web App name."
}

output "default_host_name" {
  value       = azurerm_static_web_app.this.default_host_name
  description = "Default SWA host name."
}

output "api_key" {
  value       = azurerm_static_web_app.this.api_key
  description = "SWA deployment API key."
  sensitive   = true
}
