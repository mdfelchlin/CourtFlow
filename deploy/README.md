# Deploy scripts

## `deploy-dev.ps1`

End-to-end **dev** deploy from your machine: Terraform (remote state bootstrap + `envs/dev` apply) and a **local** API image build/push to ACR, then `az containerapp update` to point the dev Container App at that image. It also sets API **CORS** to the dev Static Web App host after the first apply so the origin matches the real SWA URL.

**It does not** publish the Blazor WASM app to Static Web Apps. Use the GitHub workflow [`.github/workflows/deploy-swa.yml`](../.github/workflows/deploy-swa.yml) (or your usual SWA deploy path) for the PWA.

### Prerequisites

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az`), signed in or able to `az login`
- [Terraform](https://developer.hashicorp.com/terraform/install) (>= 1.7)
- [Docker](https://docs.docker.com/get-docker/) (for `docker build` / `docker push`)
- Git (script checks for the `git` command)
- Permissions to create resources in the subscription and to push to the ACR the stack creates/uses

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `SubscriptionId` | Yes | Azure subscription ID. |
| `Location` | No (default `westus2`) | Region for Terraform resources. |
| `ProjectName` | No (default `courtflow`) | Prefix for naming (e.g. state RG, ACR). |
| `TursoConnectionString` | Yes | `ConnectionStrings__Default` for Turso/libSQL (see root [README](../README.md)). |
| `ClerkAuthority` | Yes | Clerk issuer URL for API JWT validation (`Clerk:Authority`). |
| `ClerkAudience` | Yes | Expected audience for API tokens (`Clerk:Audience`). |

### How to run

From the **repository root** (e.g. `C:\Repos\CourtFlow`):

```powershell
.\deploy\deploy-dev.ps1 `
  -SubscriptionId "00000000-0000-0000-0000-000000000000" `
  -TursoConnectionString "<your-libsql-connection-string>" `
  -ClerkAuthority "https://<your-clerk-issuer>" `
  -ClerkAudience "<your-api-audience>"
```

Optional: `-Location "westus2"`, `-ProjectName "courtflow"`.

### What the script does (order)

1. **Azure login** — uses current `az` session or runs `az login`, then `az account set`.
2. **Bootstrap** — `infra/terraform/bootstrap`: writes `terraform.tfvars`, `terraform init`, `terraform apply` (creates state RG + storage + container for remote state).
3. **Dev stack** — `infra/terraform/envs/dev`: writes `backend.hcl` and `terraform.tfvars`, `terraform init -reconfigure` with a **quoted full path** to `backend.hcl` (avoids Windows PowerShell mangling ` -backend-config=...`), then `terraform apply` (RG, Log Analytics, ACR, Container Apps env, API app, SWA, budget).
4. **CORS** — `terraform plan` + `terraform apply` of a saved plan in `envs/dev` with `cors_origins` set to `https://<dev-swa-default-host>`.
5. **API image** — from repo root: `az acr login`, `docker build` with [`Dockerfile.api`](Dockerfile.api), `docker push` to `courtflow-api:<timestamp>`.
6. **Container App** — `az containerapp update` to the new image; prints API URL and SWA host.

### Files written locally

The script overwrites (do not commit secrets):

- `infra/terraform/bootstrap/terraform.tfvars`
- `infra/terraform/envs/dev/backend.hcl`
- `infra/terraform/envs/dev/terraform.tfvars`

Keep these paths out of version control if they contain real connection strings; use your own `.gitignore` rules or local-only copies as your team prefers.

### Redeploy API only (manual)

After infra exists, you only need build/push/update:

```powershell
az acr login -n <acrName>
docker build -f deploy/Dockerfile.api -t <acr>.azurecr.io/courtflow-api:manual .
docker push <acr>.azurecr.io/courtflow-api:manual
az containerapp update -g <rg> -n <app> --image <acr>.azurecr.io/courtflow-api:manual
```

Get `acrName`, resource group, and app name from Terraform outputs in `infra/terraform/envs/dev` or the Azure portal.

### Troubleshooting (Windows / Terraform)

- **`Error: Too many command line arguments. Did you mean to use -chdir?` on `terraform init`**  
  Use a **quoted** absolute path:  
  `terraform init -reconfigure -backend-config="C:\path\to\repo\infra\terraform\envs\dev\backend.hcl"`  
  from `infra\terraform\envs\dev`, or rely on the updated `deploy-dev.ps1` which does this for you.

- **`Backend initialization required, please run "terraform init"`**  
  Run the init line above (with your real `backend.hcl` path) before `terraform plan` / `apply`. If you changed `backend.hcl`, use `-reconfigure`.

- **Empty ACR / Container App in the script output**  
  The dev apply step did not complete; fix Terraform errors first. The script now stops on non-zero exit codes so Docker/`az` does not run with empty names.
