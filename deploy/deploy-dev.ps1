param(
  [Parameter(Mandatory = $true)][string]$SubscriptionId,
  [string]$Location = "westus2",
  [string]$ProjectName = "courtflow",
  [Parameter(Mandatory = $true)][string]$TursoConnectionString,
  [Parameter(Mandatory = $true)][string]$ClerkAuthority,
  [Parameter(Mandatory = $true)][string]$ClerkAudience
)

$ErrorActionPreference = "Stop"

function Assert-LastExit($step) {
  if ($LASTEXITCODE -ne 0) {
    throw "Step failed: $step (exit code $LASTEXITCODE)"
  }
}

function Require-Cmd($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Missing required command: $name"
  }
}

Write-Host "Checking prerequisites..."
Require-Cmd "az"
Require-Cmd "terraform"
Require-Cmd "docker"
Require-Cmd "git"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$tfRoot = Join-Path $repoRoot "infra/terraform"
$bootstrapDir = Join-Path $tfRoot "bootstrap"
$devDir = Join-Path $tfRoot "envs/dev"

Write-Host "Logging into Azure..."
az account show 1>$null 2>$null
if ($LASTEXITCODE -ne 0) { az login | Out-Null }
az account set --subscription $SubscriptionId
Assert-LastExit "az account set"

# 1) Bootstrap remote state
Write-Host "Bootstrapping Terraform remote state..."
Push-Location $bootstrapDir
@"
subscription_id = "$SubscriptionId"
location = "$Location"
resource_group_name = "${ProjectName}-tfstate-rg"
storage_account_name_prefix = "${ProjectName}tf"
container_name = "tfstate"
"@ | Set-Content -Path "terraform.tfvars" -Encoding UTF8

terraform init
Assert-LastExit "terraform init (bootstrap)"
terraform apply -auto-approve
Assert-LastExit "terraform apply (bootstrap)"

$backendRg = terraform output -raw backend_resource_group_name
$backendSa = terraform output -raw backend_storage_account_name
$backendContainer = terraform output -raw backend_container_name
if ([string]::IsNullOrWhiteSpace($backendRg) -or [string]::IsNullOrWhiteSpace($backendSa) -or [string]::IsNullOrWhiteSpace($backendContainer)) {
  throw "Bootstrap outputs are empty. Check Terraform errors above."
}
Pop-Location

# 2) Apply dev infrastructure
Write-Host "Applying dev infrastructure..."
Push-Location $devDir

@"
resource_group_name  = "$backendRg"
storage_account_name = "$backendSa"
container_name       = "$backendContainer"
key                  = "dev.terraform.tfstate"
"@ | Set-Content -Path "backend.hcl" -Encoding UTF8

@"
subscription_id = "$SubscriptionId"
location        = "$Location"
project_name    = "$ProjectName"
environment     = "dev"

min_replicas = 0
max_replicas = 3
api_image    = "mcr.microsoft.com/k8se/quickstart:latest"

cors_origins = [
  "https://placeholder-dev.azurestaticapps.net"
]

budget_amount = 25
budget_contact_emails = []

connection_string_default = "$TursoConnectionString"
clerk_authority           = "$ClerkAuthority"
clerk_audience            = "$ClerkAudience"

secrets = {}
"@ | Set-Content -Path "terraform.tfvars" -Encoding UTF8

# Use a quoted full path so PowerShell does not mangle -backend-config (avoids
# "Too many command line arguments" / spurious -chdir hints on Windows)
$devBackendHcl = Join-Path $devDir "backend.hcl"
terraform init -reconfigure -backend-config="$devBackendHcl"
Assert-LastExit "terraform init (dev, remote backend)"
terraform apply -auto-approve
Assert-LastExit "terraform apply (dev)"

$rg = terraform output -raw resource_group_name
$acrName = terraform output -raw acr_name
$containerApp = terraform output -raw container_app_name
$swaHost = terraform output -raw static_web_app_default_host_name
if (
  [string]::IsNullOrWhiteSpace($rg) -or
  [string]::IsNullOrWhiteSpace($acrName) -or
  [string]::IsNullOrWhiteSpace($containerApp) -or
  [string]::IsNullOrWhiteSpace($swaHost)
) {
  throw "Dev stack outputs are empty. Check Terraform apply succeeded."
}
Pop-Location

# 3) Re-apply with real SWA CORS
Write-Host "Updating CORS origin to SWA host..."
Push-Location $devDir
@"
subscription_id = "$SubscriptionId"
location        = "$Location"
project_name    = "$ProjectName"
environment     = "dev"

min_replicas = 0
max_replicas = 3
api_image    = "mcr.microsoft.com/k8se/quickstart:latest"

cors_origins = [
  "https://$swaHost"
]

budget_amount = 25
budget_contact_emails = []

connection_string_default = "$TursoConnectionString"
clerk_authority           = "$ClerkAuthority"
clerk_audience            = "$ClerkAudience"

secrets = {}
"@ | Set-Content -Path "terraform.tfvars" -Encoding UTF8

try {
  terraform plan -out=tfplan
  Assert-LastExit "terraform plan (CORS update)"
  # Saved plan is already approved; do not use -auto-approve with a plan file
  terraform apply tfplan
  Assert-LastExit "terraform apply (CORS update)"
}
finally {
  $planFile = Join-Path $devDir "tfplan"
  if (Test-Path $planFile) {
    Remove-Item $planFile -Force -ErrorAction SilentlyContinue
  }
  Pop-Location
}

# 4) Build and push API image
Write-Host "Building and pushing API image..."
$imageTag = (Get-Date -Format "yyyyMMddHHmmss")
$image = "$acrName.azurecr.io/courtflow-api:$imageTag"

Push-Location $repoRoot
az acr login --name $acrName
Assert-LastExit "az acr login"
docker build -f deploy/Dockerfile.api -t $image .
Assert-LastExit "docker build"
docker push $image
Assert-LastExit "docker push"
Pop-Location

# 5) Update Container App
Write-Host "Updating Container App image..."
az containerapp update `
  --name $containerApp `
  --resource-group $rg `
  --image $image | Out-Null
Assert-LastExit "az containerapp update"

$fqdn = az containerapp show --name $containerApp --resource-group $rg --query properties.configuration.ingress.fqdn -o tsv
Assert-LastExit "az containerapp show"

Write-Host ""
Write-Host "Dev deploy complete."
Write-Host "Resource Group: $rg"
Write-Host "ACR: $acrName"
Write-Host "Container App: $containerApp"
Write-Host "API URL: https://$fqdn"
Write-Host "SWA Host: https://$swaHost"
