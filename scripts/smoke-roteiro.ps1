#requires -Version 5.1
<#
.SYNOPSIS
  Smoke local do roteiro das 21 operações do contrato (README). Usa um banco descartável no SQL Server do compose.
.DESCRIPTION
  Pré-requisitos: .env com MSSQL_SA_PASSWORD, `docker compose up -d --wait sqlserver`, `dotnet build -c Release`.
  Cria o banco FinTrackSmoke_<guid>, aplica as migrations, sobe a API, executa o roteiro, confere os códigos HTTP
  esperados, derruba a API e remove o banco. Falha (exit 1) no primeiro código HTTP inesperado.
#>
[CmdletBinding()]
param([int]$Port = 5099)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
$api = $null
$database = "FinTrackSmoke_$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$base = "http://127.0.0.1:$Port/api/v1"
$count = 0
$failed = $false

function Read-DotEnv {
    $values = @{}
    if (Test-Path '.env') {
        foreach ($line in Get-Content '.env') {
            $t = $line.Trim()
            $i = $t.IndexOf('=')
            if (-not $t.StartsWith('#') -and $i -gt 0) { $values[$t.Substring(0, $i).Trim()] = $t.Substring($i + 1).Trim() }
        }
    }
    $values
}

function Invoke-Step {
    param([string]$Operation, [string]$Method, [string]$Path, [string]$Body, [int]$Expected)
    $out = [IO.Path]::GetTempFileName()
    try {
        $args = @('-s', '-X', $Method, '-o', $out, '-w', '%{http_code}', "$base$Path")
        if ($Body) {
            $code = $Body | & curl.exe @args -H 'Content-Type: application/json' --data-binary '@-'
        }
        else {
            $code = & curl.exe @args
        }
        $text = Get-Content $out -Raw -ErrorAction SilentlyContinue
        if ([int]$code -ne $Expected) { throw "$Operation ($Method $Path): esperado $Expected, obtido $code. Corpo: $text" }
        $script:count++
        Write-Host ("{0,2}. {1,-24} {2,-6} {3,-32} {4}" -f $script:count, $Operation, $Method, $Path, $code)
        if ($text) { return ($text | ConvertFrom-Json) }
    }
    finally { Remove-Item $out -ErrorAction SilentlyContinue }
}

function Invoke-Sql([string]$Query) {
    docker compose exec -T sqlserver /bin/bash -c "/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P `$MSSQL_SA_PASSWORD -b -Q '$Query'" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Falha SQL: $Query" }
}

try {
    $env_ = Read-DotEnv
    $password = if ($env:MSSQL_SA_PASSWORD) { $env:MSSQL_SA_PASSWORD } else { $env_['MSSQL_SA_PASSWORD'] }
    if (-not $password) { throw 'MSSQL_SA_PASSWORD ausente (ambiente ou .env).' }
    $sqlPort = if ($env_['MSSQL_PORT']) { $env_['MSSQL_PORT'] } else { '14330' }
    $connection = "Server=localhost,$sqlPort;Database=$database;User Id=sa;Password=$password;TrustServerCertificate=True"

    Write-Host "Aplicando migrations em $database..."
    dotnet ef database update --project src/FinTrack.Infrastructure --startup-project src/FinTrack.Infrastructure --connection $connection | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao aplicar migrations.' }

    $env:ConnectionStrings__FinTrack = $connection
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
    $api = Start-Process dotnet -ArgumentList 'src/FinTrack.Api/bin/Release/net10.0/FinTrack.Api.dll' -PassThru -WindowStyle Hidden
    $ready = $false
    for ($i = 0; $i -lt 40 -and -not $ready; $i++) {
        Start-Sleep -Milliseconds 500
        $ready = (& curl.exe -s -o NUL -w '%{http_code}' "http://127.0.0.1:$Port/health") -eq '200'
    }
    if (-not $ready) { throw 'API não respondeu em /health.' }

    $day = (Get-Date).AddDays(-1).ToString('yyyy-MM-dd')
    $from = (Get-Date).AddDays(-30).ToString('yyyy-MM-dd')
    $to = (Get-Date).ToString('yyyy-MM-dd')

    $housing = Invoke-Step createCategory POST /categories '{"name":"Moradia","nature":"EXPENSE"}' 201
    $salary = Invoke-Step createCategory POST /categories '{"name":"Salario","nature":"INCOME"}' 201
    $temp = Invoke-Step createCategory POST /categories '{"name":"Temp","nature":"EXPENSE"}' 201
    Invoke-Step listCategories GET /categories $null 200 | Out-Null
    Invoke-Step getCategory GET "/categories/$($housing.id)" $null 200 | Out-Null
    Invoke-Step updateCategory PATCH "/categories/$($temp.id)" '{"name":"Temporaria"}' 200 | Out-Null
    Invoke-Step deleteCategory DELETE "/categories/$($temp.id)" $null 204 | Out-Null

    $rent = Invoke-Step createAccount POST /accounts ('{"categoryId":' + $housing.id + ',"name":"Aluguel","classifier":"OF"}') 201
    $wage = Invoke-Step createAccount POST /accounts ('{"categoryId":' + $salary.id + ',"name":"Salario"}') 201
    $spare = Invoke-Step createAccount POST /accounts ('{"categoryId":' + $housing.id + ',"name":"Descartavel","classifier":"NOV"}') 201
    Invoke-Step listAccounts GET /accounts $null 200 | Out-Null
    Invoke-Step getAccount GET "/accounts/$($rent.id)" $null 200 | Out-Null
    Invoke-Step updateAccount PATCH "/accounts/$($rent.id)" '{"name":"Aluguel casa"}' 200 | Out-Null
    Invoke-Step changeAccountStatus PATCH "/accounts/$($spare.id)/status" '{"active":false}' 200 | Out-Null
    Invoke-Step deleteAccount DELETE "/accounts/$($spare.id)" $null 204 | Out-Null

    $t1 = Invoke-Step createTransaction POST /transactions ('{"accountId":' + $rent.id + ',"amount":1800.00,"realizedAt":"' + $day + '","description":"Aluguel"}') 201
    Invoke-Step createTransaction POST /transactions ('{"accountId":' + $wage.id + ',"amount":5000.00,"realizedAt":"' + $day + '"}') 201 | Out-Null
    $t3 = Invoke-Step createTransaction POST /transactions ('{"accountId":' + $wage.id + ',"amount":100.00,"realizedAt":"' + $day + '"}') 201
    Invoke-Step listTransactions GET '/transactions?pageSize=10' $null 200 | Out-Null
    Invoke-Step getTransaction GET "/transactions/$($t1.id)" $null 200 | Out-Null
    Invoke-Step updateTransaction PATCH "/transactions/$($t1.id)" '{"amount":1850.00}' 200 | Out-Null
    Invoke-Step listAccountTransactions GET "/accounts/$($rent.id)/transactions" $null 200 | Out-Null
    Invoke-Step deleteTransaction DELETE "/transactions/$($t3.id)" $null 204 | Out-Null

    $summary = Invoke-Step getFinancialSummary GET "/reports/summary?from=$from&to=$to" $null 200
    Invoke-Step getReportByCategory GET "/reports/by-category?from=$from&to=$to" $null 200 | Out-Null
    Invoke-Step getReportByAccount GET "/reports/by-account?from=$from&to=$to" $null 200 | Out-Null
    Invoke-Step getReportByClassifier GET "/reports/by-classifier?from=$from&to=$to" $null 200 | Out-Null

    if ($summary.income -ne 5000 -or $summary.expenses -ne 1850 -or $summary.balance -ne 3150) {
        throw "Resumo inesperado: receitas $($summary.income), despesas $($summary.expenses), resultado $($summary.balance)."
    }
    Write-Host "`nChamadas executadas: $count (cobrem as 21 operações distintas)."
    Write-Host 'SMOKE PASS: as 21 operações do contrato responderam como esperado.' -ForegroundColor Green
}
catch {
    Write-Host "SMOKE FAIL: $($_.Exception.Message)" -ForegroundColor Red
    $failed = $true
}
finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force }
    try { Invoke-Sql "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]" } catch { }
    Pop-Location
}
if ($failed) { exit 1 }
