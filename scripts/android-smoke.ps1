#Requires -Version 7.0
# Fumaça do app no emulador (CA-TEN-005). Antes: setup-android.ps1, Docker ligado, o emulador aberto
# e o Appium instalado (npm install -g appium@3.8.0; appium driver install uiautomator2@8.7.0).
# O script sobe o Postgres, a API (porta 5080) e o Appium (porta 4723): pare a sua API antes.
# A API sobe com o perfil http e lê as strings de conexão dos user-secrets (setup-dev.ps1) ou das
# variáveis de ambiente ConnectionStrings__App e ConnectionStrings__Owner, se já definidas.
# O código de saída é o do dotnet test. Os logs da API e do Appium ficam em appium-logs/.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# O pwsh não para sozinho quando um comando nativo falha: cada um tem o código de saída conferido.
function Assert-NativeSuccess([string] $Message) {
    if ($LASTEXITCODE -ne 0) { throw "$Message (código de saída $LASTEXITCODE)." }
}

# O setup-android.ps1 grava ANDROID_HOME e JAVA_HOME no escopo User; um terminal aberto antes dele não as vê.
foreach ($name in 'ANDROID_HOME', 'JAVA_HOME') {
    if (-not [Environment]::GetEnvironmentVariable($name, 'Process')) {
        [Environment]::SetEnvironmentVariable($name, [Environment]::GetEnvironmentVariable($name, 'User'), 'Process')
    }
    if (-not [Environment]::GetEnvironmentVariable($name, 'Process')) {
        throw "$name não definida; rode scripts/setup-android.ps1."
    }
}

# Falha cedo, antes de subir qualquer processo: o Appium é pré-requisito manual e as portas têm de estar livres,
# senão a sonda de prontidão aceitaria o processo de outra pessoa.
if (-not (Get-Command appium.cmd -ErrorAction SilentlyContinue)) {
    throw 'Appium não encontrado no PATH; instale-o e abra um terminal novo (veja a seção "App Android" do README).'
}
foreach ($port in 5080, 4723) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "A porta $port já está em uso por outro processo; encerre-o antes (a fumaça sobe a própria API e o Appium)."
    }
}

$adb = Join-Path $env:ANDROID_HOME 'platform-tools\adb.exe'
$apk = Join-Path $root 'src/Dbvprovas.App/bin/Debug/net10.0-android/io.github.gustavo0101.dbvprovas-Signed.apk'
$logs = Join-Path $root 'appium-logs'
New-Item -ItemType Directory -Force $logs | Out-Null

# Falha cedo, com mensagem clara, se não houver emulador pronto.
$deadline = (Get-Date).AddMinutes(2)
while ((& $adb shell getprop sys.boot_completed 2>$null) -ne '1') {
    if ((Get-Date) -gt $deadline) { throw 'Nenhum emulador pronto; abra o emulador dbvprovas antes (veja o README).' }
    Start-Sleep -Seconds 2
}

docker compose -f (Join-Path $root 'compose.yaml') up -d --wait postgres
Assert-NativeSuccess 'Falha ao subir o Postgres com o docker compose'
dotnet build (Join-Path $root 'src/Dbvprovas.App') -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true
Assert-NativeSuccess 'Falha ao compilar o app'
dotnet build (Join-Path $root 'src/Dbvprovas.Api')
Assert-NativeSuccess 'Falha ao compilar a API'
& $adb install -r $apk
Assert-NativeSuccess 'Falha ao instalar o APK no emulador'

$api = $null
$appium = $null
$exitCode = 1
try {
    # Os dois processos sobem dentro do try: se o segundo falhar, o finally ainda encerra o primeiro.
    # Os argumentos são relativos à raiz (-WorkingDirectory), porque o Start-Process não põe aspas neles.
    $api = Start-Process dotnet -ArgumentList @('run', '--no-build', '--project', 'src/Dbvprovas.Api', '--launch-profile', 'http') `
        -WorkingDirectory $root -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logs 'api.out.log') -RedirectStandardError (Join-Path $logs 'api.err.log')
    $appium = Start-Process appium.cmd -ArgumentList @('--address', '127.0.0.1', '--log', 'appium-logs/appium.log') `
        -WorkingDirectory $root -PassThru -WindowStyle Hidden

    foreach ($wait in @(@{ Url = 'http://localhost:5080/health/ready'; Process = $api }, @{ Url = 'http://127.0.0.1:4723/status'; Process = $appium })) {
        $deadline = (Get-Date).AddMinutes(2)
        while ($true) {
            try { Invoke-WebRequest $wait.Url -UseBasicParsing -TimeoutSec 5 | Out-Null; break }
            catch {
                if ($wait.Process.HasExited) { throw "O processo encerrou antes de responder: $($wait.Url) (veja appium-logs/)." }
                if ((Get-Date) -gt $deadline) { throw "Não respondeu a tempo: $($wait.Url)" }
                Start-Sleep -Seconds 2
            }
        }
        # Quem respondeu tem de ser o processo deste script, não outro que ocupava a porta.
        if ($wait.Process.HasExited) { throw "A porta de $($wait.Url) estava ocupada por outro processo: o do script já tinha encerrado (veja appium-logs/)." }
    }

    $env:DBV_APPIUM_URL = 'http://127.0.0.1:4723/'
    dotnet test (Join-Path $root 'tests/Dbvprovas.App.Smoke')
    $exitCode = $LASTEXITCODE
}
finally {
    # O dotnet run e o Appium sobem processos filhos: encerra a árvore inteira.
    # O taskkill não pode mascarar o resultado do teste: o código de saída já foi guardado.
    foreach ($process in $api, $appium) {
        if ($process) { & taskkill /PID $process.Id /T /F 2>&1 | Out-Null }
    }
}

exit $exitCode
