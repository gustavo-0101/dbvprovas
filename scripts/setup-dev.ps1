# Prepara a máquina de dev (D-109): hooks do PC-16 e a lista local de termos.
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
git -C $root config pc16.termsFile (Resolve-Path $TermsFile).Path
Write-Host 'Hooks do PC-16 ligados.'
