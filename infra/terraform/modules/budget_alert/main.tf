resource "azurerm_consumption_budget_resource_group" "this" {
  name              = var.name
  resource_group_id = var.resource_group_id

  amount     = var.amount
  time_grain = var.time_grain

  time_period {
    start_date = var.start_date
    end_date   = var.end_date
  }

  # At least one notification block is required. With no emails, notify subscription RG owners by role.
  dynamic "notification" {
    for_each = length(var.contact_emails) > 0 ? [1] : []
    content {
      enabled        = true
      threshold      = var.threshold
      operator       = "GreaterThan"
      threshold_type = "Actual"
      contact_emails = var.contact_emails
    }
  }

  dynamic "notification" {
    for_each = length(var.contact_emails) == 0 ? [1] : []
    content {
      enabled        = true
      threshold      = var.threshold
      operator       = "GreaterThan"
      threshold_type = "Actual"
      contact_roles  = var.fallback_notification_roles
    }
  }
}
