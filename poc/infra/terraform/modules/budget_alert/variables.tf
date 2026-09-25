variable "name" {
  type        = string
  description = "Budget name."
}

variable "resource_group_id" {
  type        = string
  description = "Resource group ID the budget applies to."
}

variable "amount" {
  type        = number
  description = "Monthly budget amount in subscription currency."
}

variable "time_grain" {
  type        = string
  description = "Budget time grain."
  default     = "Monthly"
}

variable "start_date" {
  type        = string
  description = "Budget start date in RFC3339 format."
}

variable "end_date" {
  type        = string
  description = "Budget end date in RFC3339 format."
}

variable "threshold" {
  type        = number
  description = "Alert threshold percentage."
  default     = 80
}

variable "contact_emails" {
  type        = list(string)
  description = "Budget notification recipients. If empty, contact_roles is used for the required notification block."
  default     = []
}

variable "fallback_notification_roles" {
  type        = list(string)
  description = "When contact_emails is empty, notify these directory roles (e.g. Owner on the budget scope)."
  default     = ["Owner"]
}
