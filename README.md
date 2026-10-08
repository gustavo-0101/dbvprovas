# dbvprovas

Sistema de provas por especialidade para clubes de Desbravadores: API, app Android e site.

## Pré-requisitos

- .NET SDK 10.0.401 ou mais novo (o `global.json` fixa a versão).
- Git, PowerShell 7 (`pwsh`) e [gitleaks](https://github.com/gitleaks/gitleaks).

## Verificação antes do commit

Os hooks em `.githooks/` barram segredos, e-mails fora dos domínios reservados e os termos de uma lista local, que fica fora deste repositório. Para ligá-los:

```powershell
pwsh scripts/setup-dev.ps1 -TermsFile <caminho da lista local>
```

Para conferir o repositório inteiro: `dotnet run --file tools/pc16-check.cs -- all`.

## Ambiente de dev

Requer o Docker Desktop ligado.

```powershell
pwsh scripts/setup-dev.ps1 -TermsFile <caminho da lista local>   # gera o .env e grava as strings de conexão
docker compose up -d --wait
dotnet run --project src/Dbvprovas.Api --launch-profile http
```

- API e site: http://localhost:5080 (mesma origem). Em dev, a API aplica as migrations e um seed fictício ao subir, e o site oferece a entrada com as contas fictícias.
- Saúde: http://localhost:5080/health/ready
- Logs, traces e métricas (Aspire Dashboard): http://localhost:18888

## Testes

```powershell
dotnet test tests/Dbvprovas.Api.Tests      # Postgres em contêiner (Testcontainers)
dotnet test tests/Dbvprovas.Tools.Tests    # verificador e hooks (exige o gitleaks)
dotnet test tests/Dbvprovas.Ui.Tests       # componentes (bUnit)
dotnet build tests/Dbvprovas.Web.E2E; pwsh tests/Dbvprovas.Web.E2E/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/Dbvprovas.Web.E2E --no-build   # caminho feliz no navegador (Playwright); também exige o Docker (Testcontainers)
```

## App Android

Requer o ambiente Android (uma vez: `pwsh scripts/setup-android.ps1`) e a API rodando. Compilar a solução inteira (`Dbvprovas.slnx`) também exige o workload `maui-android`, porque o app faz parte dela.

```powershell
& "$env:ANDROID_HOME\emulator\emulator.exe" -avd dbvprovas -gpu swiftshader_indirect -no-metrics
dotnet build src/Dbvprovas.App -t:Run -f net10.0-android
```

No emulador, o app fala com a API da máquina de dev em `http://10.0.2.2:5080`.

Fumaça com Appium (exige `npm install -g appium@3.8.0` e `appium driver install uiautomator2@8.7.0`). Ela sobe o Postgres e a própria API, então a sua API precisa estar parada antes:

```powershell
pwsh scripts/android-smoke.ps1
```

## Licença

[AGPL-3.0](LICENSE).

## Contribuições

Este é um projeto pessoal. Contribuições externas não são aceitas por enquanto.
