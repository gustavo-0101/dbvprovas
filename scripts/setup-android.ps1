#Requires -Version 7.0
# Monta o ambiente Android pela linha de comando (D-114): JDK 21, workload do MAUI, Android SDK
# e o emulador "dbvprovas". Uso, uma vez por máquina: pwsh scripts/setup-android.ps1
# Pode rodar de novo: o que já está instalado é reaproveitado (só o emulador "dbvprovas" é recriado).
$ErrorActionPreference = 'Stop'
$sdk = Join-Path $env:LOCALAPPDATA 'Android\Sdk'
$image = 'system-images;android-34;google_apis;x86_64'
$jdkParent = Join-Path $env:ProgramFiles 'Microsoft'

# O pwsh não para sozinho quando um comando nativo falha: cada um tem o código de saída conferido.
function Assert-NativeSuccess([string] $Message) {
    if ($LASTEXITCODE -ne 0) { throw "$Message (código de saída $LASTEXITCODE)." }
}

# Com um emulador aberto, o sdkmanager e o avdmanager não conseguem trocar arquivos em uso.
# O processo é o qemu-system-x86_64, ou o -headless quando o emulador abre com -no-window.
if (Get-Process -Name 'qemu-system-x86_64', 'qemu-system-x86_64-headless' -ErrorAction SilentlyContinue) {
    throw 'Há um emulador aberto; feche a janela do emulador (ou rode adb emu kill com o adb do ANDROID_HOME) e rode o script de novo. O emulador "dbvprovas" é recriado do zero.'
}

# Ordena pastas pelo número de versão do nome (21.0.12 vem depois de 21.0.8); sem número, vai por último.
function Get-VersionKey([string] $Name) {
    $match = [regex]::Match($Name, '\d+(\.\d+){1,3}')
    if ($match.Success) { [version] $match.Value } else { [version] '0.0' }
}

# Um JDK vale se for uma pasta com bin\java.exe e bin\javac.exe (um JRE não compila o app) e o arquivo release disser versão 21.
# Qualquer valor inválido (vazio, entre aspas, drive que não existe) devolve $false: nunca lança.
function Test-Jdk21([string] $Path) {
    try {
        $Path = "$Path".Trim().Trim('"')
        if (-not $Path -or -not (Test-Path -LiteralPath $Path -PathType Container)) { return $false }
        foreach ($tool in 'bin\java.exe', 'bin\javac.exe') {
            if (-not (Test-Path -LiteralPath (Join-Path $Path $tool))) { return $false }
        }
        $release = Join-Path $Path 'release'
        (Test-Path -LiteralPath $release) -and [bool](Select-String -LiteralPath $release -Pattern '^JAVA_VERSION="21("|\.)' -Quiet)
    }
    catch { $false }
}

# Primeiro o JAVA_HOME (do processo ou do usuário), que pode apontar para fora do caminho padrão.
function Find-Jdk {
    foreach ($candidate in $env:JAVA_HOME, [Environment]::GetEnvironmentVariable('JAVA_HOME', 'User')) {
        # Só caminho absoluto, porque o valor é gravado no escopo do usuário.
        $path = "$candidate".Trim().Trim('"').Trim()
        if (-not $path -or -not [IO.Path]::IsPathFullyQualified($path)) { continue }
        $path = [IO.Path]::TrimEndingDirectorySeparator($path)
        if (Test-Jdk21 $path) { return $path }
    }
    if (-not (Test-Path $jdkParent)) { return }
    Get-ChildItem $jdkParent -Directory -Filter 'jdk-21*' |
        Where-Object { Test-Jdk21 $_.FullName } |
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
$jdkInstalledNow = $false
if ($jdk) {
    Write-Host "JDK 21 já instalado em $jdk."
}
else {
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw 'winget não encontrado; instale o JDK 21 (Microsoft OpenJDK) e rode o script de novo.'
    }
    winget install --id Microsoft.OpenJDK.21 --exact --accept-source-agreements --accept-package-agreements
    Assert-NativeSuccess 'Falha ao instalar o JDK 21 com o winget; se ele já estiver instalado fora do caminho padrão, aponte o JAVA_HOME para a pasta dele e rode de novo'
    $jdkInstalledNow = $true
    $jdk = Find-Jdk
    if (-not $jdk) { throw "O winget terminou, mas nenhum JDK 21 apareceu em $jdkParent; se ele foi instalado em outro lugar, aponte o JAVA_HOME para a pasta dele e rode de novo." }
}

dotnet workload install maui-android
Assert-NativeSuccess 'Falha ao instalar o workload maui-android'

# O próprio build do .NET instala o SDK; um projeto descartável basta para isso.
# O projeto descartável vai numa pasta nova a cada execução; apagá-la no fim é só limpeza e não derruba o script.
$probe = Join-Path $env:TEMP ('dbv-android-deps-' + [guid]::NewGuid().ToString('N'))
try {
    $created = dotnet new android -o $probe 2>&1
    if ($LASTEXITCODE -ne 0) { $created | Write-Host }
    Assert-NativeSuccess 'Falha ao criar o projeto descartável do Android'
    dotnet build $probe -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=$sdk" "-p:JavaSdkDirectory=$jdk" '-p:AcceptAndroidSDKLicenses=True'
    Assert-NativeSuccess 'Falha ao instalar o Android SDK pelo build do .NET'
}
finally {
    if (Test-Path $probe) {
        Remove-Item -Recurse -Force $probe -ErrorAction SilentlyContinue
        if (Test-Path $probe) { Write-Warning "Não foi possível apagar $probe; apague a pasta à mão." }
    }
}

[Environment]::SetEnvironmentVariable('ANDROID_HOME', $sdk, 'User')
[Environment]::SetEnvironmentVariable('JAVA_HOME', $jdk, 'User')
$env:ANDROID_HOME = $sdk
$env:JAVA_HOME = $jdk
Write-Host "Variáveis de usuário gravadas: ANDROID_HOME = $sdk ; JAVA_HOME = $jdk"
Write-Host 'Só terminais abertos depois deste ponto enxergam essas variáveis: abra um terminal novo antes de usar o emulador e a fumaça.'

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

# -gpu swiftshader_indirect: a WebView do app cai com a GPU padrão; -no-metrics: evita a pergunta de coleta de métricas do emulador.
$launch = "& '$emulator' -avd dbvprovas -gpu swiftshader_indirect -no-metrics"
if (($accelExit -eq 0) -and [bool]($accel -match 'is installed and usable')) {
    Write-Host "Pronto. Para abrir o emulador: $launch"
}
else {
    Write-Warning ("O emulador está instalado, mas a aceleração por hardware não foi confirmada (saída acima). " +
        "Ligue você mesmo a 'Plataforma do Hipervisor do Windows' (Windows Hypervisor Platform) em 'Ativar ou desativar recursos do Windows' " +
        "e reinicie o computador; se ela já estiver ligada, confira a virtualização (Intel VT-x ou AMD-V) no BIOS. " +
        "Este script não liga recursos do Windows nem edita o PATH por conta própria; " +
        $(if ($jdkInstalledNow) { "só o instalador do JDK, que rodou agora, ajustou o PATH e o JAVA_HOME da máquina." } else { "o JDK já estava instalado e nada foi mexido no PATH." }))
    Write-Host "Instalação concluída, mas ainda sem aceleração. Depois de resolver, confira com: & '$emulator' -accel-check"
    Write-Host "e abra o emulador com: $launch"
}
