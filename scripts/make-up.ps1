# Cashflow — wrapper PowerShell para os alvos do Makefile (07 §6).
#
# Compatibilidade: Windows PowerShell 5.1 e PowerShell 7+ (`pwsh`).
# Em PS 5.1, redirecionar stderr de comandos nativos via `2>&1` causa
# NativeCommandError. Este script NÃO usa redirecionamento de stderr
# e mantém `$ErrorActionPreference = 'Continue'` para que a stderr de
# `docker pull/build` (que é progresso, não erro) não aborte o pipeline.
# Erros reais são detectados por `$LASTEXITCODE` após cada invocação.
#
# Uso:
#   ./scripts/make-up.ps1                # = `make up`     (core + app)
#   ./scripts/make-up.ps1 up-core        # = `make up-core`
#   ./scripts/make-up.ps1 up-tools       # = `make up-tools`
#   ./scripts/make-up.ps1 down           # = `make down`
#   ./scripts/make-up.ps1 nuke           # = `make nuke`   (apaga volumes!)
#   ./scripts/make-up.ps1 logs gateway   # = `make logs SERVICE=gateway`
#   ./scripts/make-up.ps1 build          # builda imagens .NET
#   ./scripts/make-up.ps1 seed           # popula 30 dias x 20 entries
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

# Continue (não Stop) para que stderr nativa de `docker pull` (linhas de
# progresso interpretadas como exception em PS 5.1) não aborte o script.
# A integridade é mantida via Invoke-Docker abaixo.
$ErrorActionPreference = 'Continue'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$composeFile = Join-Path $repoRoot 'infra\docker-compose.yml'
$envFile = Join-Path $repoRoot 'infra\.env'

if (-not (Test-Path $envFile)) {
    Write-Warning "infra/.env não existe. Copiando de infra/.env.example..."
    Copy-Item (Join-Path $repoRoot 'infra\.env.example') $envFile
}

# Invoca docker e checa exit code manualmente.
# NÃO redireciona stderr — em PS 5.1 isso wrappa cada linha de progresso
# do docker em ErrorRecord (NativeCommandError) e quebra o pipeline.
function Invoke-Docker {
    param([Parameter(ValueFromRemainingArguments = $true)] [string[]]$Args)
    & docker @Args
    if ($LASTEXITCODE -ne 0) {
        throw "docker $($Args -join ' ') falhou com exit code $LASTEXITCODE"
    }
}

$composeArgs = @('compose', '-f', $composeFile, '--env-file', $envFile)
$composeArgsAll = $composeArgs + @('--profile','core','--profile','app','--profile','tools','--profile','perf')

switch ($Target) {
    'up' {
        Invoke-Docker @composeArgs --profile core --profile app up -d --wait
    }
    'up-core' {
        Invoke-Docker @composeArgs --profile core up -d --wait
    }
    'up-tools' {
        Invoke-Docker @composeArgs --profile core --profile tools up -d --wait
    }
    'down' {
        Invoke-Docker @composeArgsAll down --remove-orphans
    }
    'nuke' {
        Write-Host "ATENÇÃO: isso apaga TODOS os volumes (dados Postgres/Mongo/Rabbit/Redis/Keycloak)." -ForegroundColor Yellow
        $confirm = Read-Host "Digite 'yes' para confirmar"
        if ($confirm -eq 'yes') {
            Invoke-Docker @composeArgsAll down -v --remove-orphans
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
        Invoke-Docker @composeArgs --profile app build
    }
    'seed' {
        # Obtém token de admin automaticamente (admin@cashflow.local / admin123).
        # Override via $env:TOKEN se quiser usar outro usuário.
        $token = $env:TOKEN
        if (-not $token) {
            Write-Host "Obtendo token admin via Keycloak..."
            $body = @{
                grant_type    = 'password'
                client_id     = 'cashflow-api'
                client_secret = 'cashflow-api-secret'
                username      = 'admin@cashflow.local'
                password      = 'admin123'
                scope         = 'openid'
            }
            $token = (Invoke-RestMethod -Method Post `
                -Uri 'http://localhost:8080/realms/cashflow/protocol/openid-connect/token' `
                -ContentType 'application/x-www-form-urlencoded' -Body $body).access_token
        }
        $days = if ($Rest -and $Rest.Count -ge 1) { [int]$Rest[0] } else { 30 }
        $perDay = if ($Rest -and $Rest.Count -ge 2) { [int]$Rest[1] } else { 20 }
        Write-Host "Seed: $days dias x $perDay entries..."
        $r = Invoke-RestMethod -Method Post `
            -Uri 'http://localhost:8000/ledger/admin/seed' `
            -Headers @{ 'Authorization' = "Bearer $token" } `
            -ContentType 'application/json' `
            -Body "{`"days`":$days,`"entriesPerDay`":$perDay}"
        $r | ConvertTo-Json
    }
    'perf' {
        Invoke-Docker @composeArgs --profile perf run --rm k6 run /scripts/balance-50rps.js
    }
    'chaos' {
        Invoke-Docker @composeArgs stop consolidation-api consolidation-worker
    }
    'restore' {
        Invoke-Docker @composeArgs start consolidation-api consolidation-worker
    }
    'test' {
        Push-Location $repoRoot
        try {
            & dotnet test
            if ($LASTEXITCODE -ne 0) { throw "dotnet test falhou com exit code $LASTEXITCODE" }
        } finally { Pop-Location }
    }
}
