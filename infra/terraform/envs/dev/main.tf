locals {
  suffix = "${var.project_name}-${var.environment}"
  # Monthly budgets: start must not be before the current calendar month (API requirement).
  # Default = first day of the month in UTC at plan time (overridable with budget_start_date in tfvars).
  # (Avoid putting bare "T" in formatdate() — it is not a valid format verb; append the RFC3339 time as a string.)
  budget_start = coalesce(
    var.budget_start_date,
    "${formatdate("YYYY-MM", plantimestamp())}-01T00:00:00Z"
  )
  tags = {
    project     = var.project_name
    environment = var.environment
    managedBy   = "terraform"
  }

  env_vars = {
    ASPNETCORE_ENVIRONMENT = title(var.environment)
    Clerk__Authority       = var.clerk_authority
    Clerk__Audience        = var.clerk_audience
    Cors__Origins__0       = length(var.cors_origins) > 0 ? var.cors_origins[0] : ""
  }

  secret_env_vars = var.connection_string_default != "" ? {
    ConnectionStrings__Default = "connection-strings-default"
  } : {}

  all_secrets = var.connection_string_default != "" ? merge(
    var.secrets,
    { "connection-strings-default" = var.connection_string_default }
  ) : var.secrets
}

module "resource_group" {
  source   = "../../modules/resource_group"
  name     = "${local.suffix}-rg"
  location = var.location
  tags     = local.tags
}

module "log_analytics" {
  source              = "../../modules/log_analytics"
  name                = "${replace(local.suffix, "-", "")}logs"
  location            = module.resource_group.location
  resource_group_name = module.resource_group.name
  tags                = local.tags
}

module "acr" {
  source              = "../../modules/acr"
  name                = "${replace(local.suffix, "-", "")}acr"
  location            = module.resource_group.location
  resource_group_name = module.resource_group.name
  sku                 = "Basic"
  admin_enabled       = false
  tags                = local.tags
}

module "container_apps_env" {
  source                     = "../../modules/container_apps_env"
  name                       = "${local.suffix}-env"
  location                   = module.resource_group.location
  resource_group_name        = module.resource_group.name
  log_analytics_workspace_id = module.log_analytics.id
  tags                       = local.tags

  depends_on = [time_sleep.microsoft_app_propagation]
}

module "container_app_api" {
  source                       = "../../modules/container_app_api"
  name                         = "${local.suffix}-api"
  location                     = module.resource_group.location
  resource_group_name          = module.resource_group.name
  container_app_environment_id = module.container_apps_env.id
  image                        = var.api_image
  target_port                  = 8080
  min_replicas                 = var.min_replicas
  max_replicas                 = var.max_replicas
  env_vars                     = local.env_vars
  secret_env_vars              = local.secret_env_vars
  secrets                      = local.all_secrets
  acr_id                       = module.acr.id
  acr_login_server             = module.acr.login_server
  tags                         = local.tags
}

module "static_web_app" {
  source              = "../../modules/static_web_app"
  name                = "${local.suffix}-web"
  location            = module.resource_group.location
  resource_group_name = module.resource_group.name
  sku_tier            = "Free"
  sku_size            = "Free"
  tags                = local.tags
}

module "budget_alert" {
  source             = "../../modules/budget_alert"
  name               = "${local.suffix}-budget"
  resource_group_id  = module.resource_group.id
  amount             = var.budget_amount
  start_date         = local.budget_start
  end_date           = var.budget_end_date
  threshold          = 80
  contact_emails     = var.budget_contact_emails
}
