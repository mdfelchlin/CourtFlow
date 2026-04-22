# CourtFlow Terraform (Azure)

This directory provisions baseline Azure infrastructure for CourtFlow in `dev` and `prod`:

- Resource Group
- Log Analytics Workspace
- Azure Container Registry (Basic)
- Azure Container Apps Environment
- API Container App skeleton (port `8080`, min replicas `0`, capped max replicas)
- Azure Static Web App
- Resource Group budget alert

## Directory Layout

- `bootstrap/`: one-time state backend resources (Storage Account + blob container)
- `modules/`: reusable Terraform modules
- `envs/dev/`: dev environment stack
- `envs/prod/`: prod environment stack

## Prerequisites

- Terraform `>= 1.7`
- Azure CLI authenticated to the target tenant/subscription
- Permissions to create resources + assign RBAC

## Step 1: Bootstrap remote state backend

```powershell
cd infra/terraform/bootstrap
copy terraform.tfvars.example terraform.tfvars
terraform init
terraform apply
terraform output
```

Capture these outputs:

- `backend_resource_group_name`
- `backend_storage_account_name`
- `backend_container_name`

## Step 2: Initialize and apply dev

```powershell
cd ../envs/dev
copy backend.hcl.example backend.hcl
copy terraform.tfvars.example terraform.tfvars

terraform init -backend-config=backend.hcl
terraform plan
terraform apply
```

## Step 3: Initialize and apply prod

```powershell
cd ../prod
copy backend.hcl.example backend.hcl
copy terraform.tfvars.example terraform.tfvars

terraform init -backend-config=backend.hcl
terraform plan
terraform apply
```

## Secrets and Sensitive Inputs

Do not commit sensitive values. Provide in `terraform.tfvars` (excluded by gitignore) or `TF_VAR_*`:

- `connection_string_default` (Turso/libSQL)
- `secrets` map (if additional Container App secrets are needed)
- `clerk_authority`
- `clerk_audience`

## GitHub CI/CD Handoff

After apply, map Terraform outputs to GitHub Actions secrets/variables:

- `AZURE_RESOURCE_GROUP` -> `resource_group_name`
- `AZURE_ACR_NAME` -> `acr_name`
- `AZURE_CONTAINER_APP` -> `container_app_name`
- `AZURE_STATIC_WEB_APPS_API_TOKEN` -> `static_web_app_api_key` (sensitive)

Keep using OIDC for:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

## Operational Notes

- Container App image is intentionally bootstrap-only in Terraform and ignores image tag drift.
- CI should continue to deploy with `az containerapp update --image ...`.
- For CORS, set `cors_origins` to the exact SWA host names.
- **Budget:** `budget_start_date` is optional; if unset, the first day of the **current** month (UTC) at plan time is used, which satisfies Azure’s rule that monthly budget start must not be before the current month.
- **Budget notifications:** if `budget_contact_emails` is empty, notifications go to **Owner** role on the resource group (see `modules/budget_alert`).

## Troubleshooting

- **`MissingSubscriptionRegistration` / `Microsoft.App`:** `envs/dev` and `envs/prod` register `Microsoft.App` and wait before creating the Container Apps environment. Run `terraform init -upgrade` after pulling, then `terraform apply` again. Or register manually: `az provider register --namespace Microsoft.App --wait` (can take a few minutes), then apply. If you previously had `azurerm_resource_provider_registration` only in the **bootstrap** state, run `terraform state rm azurerm_resource_provider_registration.microsoft_app` under `bootstrap/` (so the next bootstrap apply does not try to **destroy** provider registration in Azure), then `terraform init -upgrade` and `apply` in `envs/dev`.

- **`Start date for monthly time grain should not be prior to current month`:** Set `budget_start_date` in `terraform.tfvars` to the first day of the current month (UTC), or leave it unset to use the automatic default above.
