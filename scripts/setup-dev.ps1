# Prepara a máquina de dev (D-109, RNF-AUD-003): hooks do PC-16, lista local, senhas do Postgres
# de dev no .env e as strings de conexão da API nos segredos de usuário do .NET (PC-10).
# Uso: pwsh scripts/setup-dev.ps1 -TermsFile <caminho da lista local>
param(
    [Parameter(Mandatory = $true)]
    [string] $TermsFile
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $TermsFile)) { throw "Lista local não encontrada: $TermsFile" }
if (-not (Get-Command gitleaks -ErrorAction SilentlyContinue)) { throw 'Instale o gitleaks: winget install --id Gitleaks.Gitleaks --exact' }

git -C $root config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) { throw 'Falha ao configurar core.hooksPath.' }
git -C $root config pc16.termsFile (Resolve-Path $TermsFile).Path
if ($LASTEXITCODE -ne 0) { throw 'Falha ao configurar pc16.termsFile.' }
Write-Host 'Hooks do PC-16 ligados.'

function New-DevSecret {
    [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)).ToLowerInvariant()
}

$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) {
    @(
        "POSTGRES_PASSWORD=$(New-DevSecret)"
        "DBV_OWNER_PASSWORD=$(New-DevSecret)"
        "DBV_APP_PASSWORD=$(New-DevSecret)"
    ) | Set-Content -Path $envFile -Encoding utf8NoBOM
    Write-Host 'Senhas de dev geradas no .env (ignorado pelo git).'
}

$values = @{}
foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*([A-Z_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
}

$api = Join-Path $root 'src/Dbvprovas.Api'
if (Test-Path $api) {
    $base = 'Host=localhost;Port=5432;Database=dbvprovas'
    dotnet user-secrets set 'ConnectionStrings:App' "$base;Username=dbv_app;Password=$($values['DBV_APP_PASSWORD'])" --project $api | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to store ConnectionStrings:App in user secrets.' }
    dotnet user-secrets set 'ConnectionStrings:Owner' "$base;Username=dbv_owner;Password=$($values['DBV_OWNER_PASSWORD'])" --project $api | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to store ConnectionStrings:Owner in user secrets.' }
    Write-Host 'Strings de conexão gravadas nos segredos de usuário da API.'
}
