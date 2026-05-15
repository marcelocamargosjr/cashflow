# Cashflow — wrapper PowerShell para os alvos do Makefile (07 §6).
#
# REQUISITO: PowerShell 7+ (`pwsh`). Em Windows PowerShell 5.1 a combinação
# de `$ErrorActionPreference = 'Stop'` com stderr nativa de `docker pull/build`
# transforma cada linha de progresso em `NativeCommandError` e aborta o script.
# Se você está em PS 5.1, rode `docker compose` diretamente (ver README §4) ou
# use WSL2 / Git Bash.
#
# Uso:
#   ./scripts/make-up.ps1                # = `make up`     (core + app)
#   ./scripts/make-up.ps1 up-core        # = `make up-core`
#   ./scripts/make-up.ps1 up-tools       # = `make up-tools`
#   ./scripts/make-up.ps1 down           # = `make down`
#   ./scripts/make-up.ps1 nuke           # = `make nuke`   (apaga volumes!)
#   ./scripts/make-up.ps1 logs gateway   # = `make logs SERVICE=gateway`
#   ./scripts/make-up.ps1 build          # builda imagens .NET
#   ./scripts/make-up.ps1 perf           # roda k6 (F7)
#   ./scripts/make-up.ps1 chaos          # derruba consolidation-* (F7)
#   ./scripts/make-up.ps1 restore        # restora consolidation-* (F7)
#   ./scripts/make-up.ps1 test           # `dotnet test`

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('up','up-core','up-tools','down','nuke','logs','build','seed','perf','chaos','restore','test')]
    [string]$Target = 'up',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$composeFile = Join-Path $repoRoot 'infra\docker-compose.yml'
$envFile = Join-Path $repoRoot 'infra\.env'

if (-not (Test-Path $envFile)) {
    Write-Warning "infra/.env não existe. Copiando de infra/.env.example..."
    Copy-Item (Join-Path $repoRoot 'infra\.env.example') $envFile
}

$composeArgs = @('compose', '-f', $composeFile, '--env-file', $envFile)
# `down`/`nuke` precisam dos profiles ativados — services profile-gated não são
# afetados por `docker compose down` sem `--profile` correspondente.
$composeArgsAll = $composeArgs + @('--profile','core','--profile','app','--profile','tools','--profile','perf')

switch ($Target) {
    'up' {
        & docker @composeArgs --profile core --profile app up -d
    }
    'up-core' {
        & docker @composeArgs --profile core up -d
    }
    'up-tools' {
        & docker @composeArgs --profile core --profile tools up -d
    }
    'down' {
        & docker @composeArgsAll down --remove-orphans
    }
    'nuke' {
        Write-Host "ATENÇÃO: isso apaga TODOS os volumes (dados Postgres/Mongo/Rabbit/Redis/Keycloak)." -ForegroundColor Yellow
        $confirm = Read-Host "Digite 'yes' para confirmar"
        if ($confirm -eq 'yes') {
            & docker @composeArgsAll down -v --remove-orphans
        } else {
            Write-Host "Cancelado." -ForegroundColor Green
        }
    }
    'logs' {
        $service = if ($Rest -and $Rest.Count -gt 0) { $Rest[0] } else { '' }
        if ($service) {
            & docker @composeArgs logs -f $service
        } else {
            & docker @composeArgs logs -f
        }
    }
    'build' {
        & docker @composeArgs --profile app build
    }
    'seed' {
        $token = $env:TOKEN
        if (-not $token) { throw 'Defina $env:TOKEN antes de rodar seed (obter via /realms/cashflow/protocol/openid-connect/token).' }
        Invoke-RestMethod -Method Post `
            -Uri 'http://localhost:8000/ledger/admin/seed' `
            -Headers @{ 'Authorization' = "Bearer $token"; 'Content-Type' = 'application/json' } `
            -Body '{"days":30,"entriesPerDay":20}'
    }
    'perf' {
        & docker @composeArgs --profile perf run --rm k6 run /scripts/balance-50rps.js
    }
    'chaos' {
        & docker @composeArgs stop consolidation-api consolidation-worker
    }
    'restore' {
        & docker @composeArgs start consolidation-api consolidation-worker
    }
    'test' {
        Push-Location $repoRoot
        try { & dotnet test } finally { Pop-Location }
    }
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
