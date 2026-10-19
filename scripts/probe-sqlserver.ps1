<#
  FinTrack v2 — probe de conectividade SQL Server do compose.yaml.
  Executa SELECT 1 dentro do container com o sqlcmd. Falha se o container não estiver saudável.
  Exemplo: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/probe-sqlserver.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    $out = & docker compose exec -T sqlserver /bin/bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $MSSQL_SA_PASSWORD -b -h -1 -Q ''SELECT 1''' 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -or $out -notmatch '(?m)^\s*1\s*$') { throw "Probe SQL Server falhou (exit $LASTEXITCODE): $out" }
    Write-Host 'SQL PROBE PASS: SELECT 1 respondeu pelo container sqlserver.' -ForegroundColor Green
}
finally { Pop-Location }
