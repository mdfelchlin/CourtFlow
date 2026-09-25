# Terraform Backend Bootstrap

This stack creates the Azure resources required for Terraform remote state:

- Resource group for state
- Storage account (Standard/LRS)
- Private blob container for state files

## Usage

```powershell
cd infra/terraform/bootstrap
copy terraform.tfvars.example terraform.tfvars
terraform init
terraform apply
```

After apply, capture outputs and use them to initialize each environment root:

```powershell
terraform output
```

Example backend init for `envs/dev`:

```powershell
terraform init `
  -backend-config="resource_group_name=<output.backend_resource_group_name>" `
  -backend-config="storage_account_name=<output.backend_storage_account_name>" `
  -backend-config="container_name=<output.backend_container_name>" `
  -backend-config="key=dev.terraform.tfstate"
```

Repeat with `key=prod.terraform.tfstate` for `envs/prod`.
