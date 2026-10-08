# Monta o ambiente Android pela linha de comando (D-114): JDK 21, workload do MAUI, Android SDK
# e o emulador "dbvprovas". Uso, uma vez por máquina: pwsh scripts/setup-android.ps1
# Pode rodar de novo: o que já está instalado é reaproveitado (só o emulador "dbvprovas" é recriado).
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$image = 'system-images;android-34;google_apis;x86_64'
$jdkParent = 'C:\Program Files\Microsoft'

# O pwsh não para sozinho quando um comando nativo falha: cada um tem o código de saída conferido.
function Assert-NativeSuccess([string] $Message) {
    if ($LASTEXITCODE -ne 0) { throw "$Message (código de saída $LASTEXITCODE)." }
}

# Ordena pastas pelo número de versão do nome (21.0.12 vem depois de 21.0.8); sem número, vai por último.
function Get-VersionKey([string] $Name) {
    $match = [regex]::Match($Name, '\d+(\.\d+){1,3}')
    if ($match.Success) { [version] $match.Value } else { [version] '0.0' }
}

function Find-Jdk {
    if (-not (Test-Path $jdkParent)) { return }
    Get-ChildItem $jdkParent -Directory -Filter 'jdk-21*' |
        Where-Object { Test-Path (Join-Path $_.FullName 'bin\java.exe') } |
        Sort-Object { Get-VersionKey $_.Name } -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

# O build do .NET põe o cmdline-tools numa pasta com o número da versão; "latest" vale se existir.
function Find-CmdlineTool([string] $Name) {
    $root = Join-Path $sdk 'cmdline-tools'
    $latest = Join-Path $root "latest\bin\$Name.bat"
    if (Test-Path $latest) { return $latest }
    if (-not (Test-Path $root)) { return }
    Get-ChildItem $root -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName "bin\$Name.bat") } |
        Sort-Object { Get-VersionKey $_.Name } -Descending |
        Select-Object -First 1 |
        ForEach-Object { Join-Path $_.FullName "bin\$Name.bat" }
}

# O winget sai com código diferente de zero quando o pacote já está instalado: só chama se faltar.
$jdk = Find-Jdk
if ($jdk) {
    Write-Host "JDK 21 já instalado em $jdk."
}
else {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw 'winget não encontrado; instale o JDK 21 (Microsoft OpenJDK) e rode o script de novo.'
    }
    winget install --id Microsoft.OpenJDK.21 --exact --accept-source-agreements --accept-package-agreements
    Assert-NativeSuccess 'Falha ao instalar o JDK 21 com o winget'
    $jdk = Find-Jdk
    if (-not $jdk) { throw "JDK 21 não encontrado em $jdkParent." }
}

dotnet workload install maui-android
Assert-NativeSuccess 'Falha ao instalar o workload maui-android'

# O próprio build do .NET instala o SDK; um projeto descartável basta para isso.
$probe = Join-Path $env:TEMP 'dbv-android-deps'
try {
    if (Test-Path $probe) { Remove-Item -Recurse -Force $probe }
    $created = dotnet new android -o $probe 2>&1
    if ($LASTEXITCODE -ne 0) { $created | Write-Host }
    Assert-NativeSuccess 'Falha ao criar o projeto descartável do Android'
    dotnet build $probe -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" '-p:AcceptAndroidSDKLicenses=True'
    Assert-NativeSuccess 'Falha ao instalar o Android SDK pelo build do .NET'
}
finally {
    if (Test-Path $probe) { Remove-Item -Recurse -Force $probe }
}

[Environment]::SetEnvironmentVariable('ANDROID_HOME', $sdk, 'User')
[Environment]::SetEnvironmentVariable('JAVA_HOME', $jdk, 'User')
$env:ANDROID_HOME = $sdk
$env:JAVA_HOME = $jdk

$sdkmanager = Find-CmdlineTool 'sdkmanager'
$avdmanager = Find-CmdlineTool 'avdmanager'
if (-not $sdkmanager -or -not $avdmanager) {
    throw "sdkmanager e avdmanager não encontrados em $sdk\cmdline-tools\<versão>\bin; confira o log do build acima."
}

1..30 | ForEach-Object { 'y' } | & $sdkmanager "--sdk_root=$sdk" --install 'emulator' 'platform-tools' $image
Assert-NativeSuccess 'Falha ao instalar o emulador e a imagem do sistema com o sdkmanager'
'no' | & $avdmanager create avd --force --name dbvprovas --package $image --device 'pixel_6'
Assert-NativeSuccess 'Falha ao criar o emulador dbvprovas com o avdmanager'

# O emulador x86_64 precisa de aceleração por hardware; o script só confere, nunca liga recurso do Windows.
$emulator = Join-Path $sdk 'emulator\emulator.exe'
if (-not (Test-Path $emulator)) { throw "emulator.exe não encontrado em $emulator." }
Write-Host 'Conferindo a aceleração do emulador (emulator -accel-check):'
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try { $accel = & $emulator -accel-check 2>&1 | ForEach-Object { "$_" } }
finally { $ErrorActionPreference = $previousPreference }
$accelExit = $LASTEXITCODE
$accel | ForEach-Object { Write-Host "  $_" }

$launch = "& '$emulator' -avd dbvprovas"
if (($accelExit -eq 0) -and [bool]($accel -match 'is installed and usable')) {
    Write-Host "Pronto. Para abrir o emulador: $launch"
}
else {
    Write-Warning ("O emulador está instalado, mas a aceleração por hardware não foi confirmada (saída acima). " +
        "Ligue você mesmo a 'Plataforma do Hipervisor do Windows' (Windows Hypervisor Platform) em 'Ativar ou desativar recursos do Windows' " +
        "e reinicie o computador; se ela já estiver ligada, confira a virtualização (Intel VT-x ou AMD-V) no BIOS. " +
        "Este script não altera recursos do Windows nem o PATH.")
    Write-Host "Instalação concluída, mas ainda sem aceleração. Depois de resolver, confira com: & '$emulator' -accel-check"
    Write-Host "e abra o emulador com: $launch"
}
