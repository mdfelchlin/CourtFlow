resource "azurerm_resource_provider_registration" "microsoft_app" {
  name = "Microsoft.App"
}

resource "time_sleep" "microsoft_app_propagation" {
  depends_on      = [azurerm_resource_provider_registration.microsoft_app]
  create_duration = "90s"
}
