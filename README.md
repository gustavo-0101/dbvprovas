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

## Licença

[AGPL-3.0](LICENSE).

## Contribuições

Este é um projeto pessoal. Contribuições externas não são aceitas por enquanto.
