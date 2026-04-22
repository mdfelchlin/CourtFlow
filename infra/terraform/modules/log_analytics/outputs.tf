output "id" {
  value       = azurerm_log_analytics_workspace.this.id
  description = "Log Analytics workspace ID."
}

output "name" {
  value       = azurerm_log_analytics_workspace.this.name
  description = "Log Analytics workspace name."
}

output "workspace_id" {
  value       = azurerm_log_analytics_workspace.this.workspace_id
  description = "Workspace customer ID."
}

output "primary_shared_key" {
  value       = azurerm_log_analytics_workspace.this.primary_shared_key
  description = "Workspace primary shared key."
  sensitive   = true
}
