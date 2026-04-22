output "resource_group_name" {
  value       = module.resource_group.name
  description = "Resource group name."
}

output "acr_name" {
  value       = module.acr.name
  description = "ACR name for GitHub secret AZURE_ACR_NAME."
}

output "acr_login_server" {
  value       = module.acr.login_server
  description = "ACR login server."
}

output "container_app_name" {
  value       = module.container_app_api.name
  description = "Container App name for GitHub secret AZURE_CONTAINER_APP."
}

output "container_app_fqdn" {
  value       = module.container_app_api.latest_revision_fqdn
  description = "Container App URL host."
}

output "container_apps_environment_name" {
  value       = module.container_apps_env.name
  description = "Container Apps environment name."
}

output "static_web_app_name" {
  value       = module.static_web_app.name
  description = "SWA name."
}

output "static_web_app_default_host_name" {
  value       = module.static_web_app.default_host_name
  description = "SWA default hostname."
}

output "static_web_app_api_key" {
  value       = module.static_web_app.api_key
  description = "SWA deployment token for GitHub secret AZURE_STATIC_WEB_APPS_API_TOKEN."
  sensitive   = true
}
