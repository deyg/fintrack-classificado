<#
  FinTrack v2 — validação ESTÁTICA de IDs e rastreabilidade do pacote.
  Não substitui parsing/lint de OpenAPI nem testes de contrato HTTP.
  Execução: powershell -NoProfile -File scripts/verify-manifest.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stepsPath = Join-Path $root 'docs/roadmap/steps.csv'
$endpointsPath = Join-Path $root 'docs/traceability/endpoints.csv'
$openapiPath = Join-Path $root 'docs/contracts/fintrack-openapi.yaml'
foreach ($path in @($stepsPath,$endpointsPath,$openapiPath)) {
    if (-not (Test-Path $path)) { throw "Falta artefato: $path" }
}
$steps = @(Import-Csv $stepsPath)
$trace = @(Import-Csv $endpointsPath)
if ($steps.Count -ne 44) { throw "Esperados 44 passos, encontrados $($steps.Count)." }
if ($trace.Count -ne 21) { throw "Esperadas 21 operações na matriz, encontradas $($trace.Count)." }
if ((@($steps.id | Sort-Object -Unique)).Count -ne $steps.Count) { throw 'IDs de passo duplicados.' }
if ((@($trace.operationId | Sort-Object -Unique)).Count -ne $trace.Count) { throw 'operationIds duplicados na matriz.' }
foreach ($s in $steps) {
    if ($s.id -notmatch '^(BOOT|CAT|ACC|TXN|REP|REL)-\d{3}$') { throw "ID inválido: $($s.id)" }
    foreach ($flag in @('requires_solution','requires_arch','requires_db','requires_http','requires_compose','requires_coverage')) {
        if ($s.$flag -notin @('0','1')) { throw "Flag inválida em $($s.id): $flag" }
    }
}
$contractText = Get-Content -Raw -Path $openapiPath -Encoding UTF8
if ($contractText -notmatch '(?m)^openapi:\s*3\.1\.0\s*$') { throw 'OpenAPI baseline deve ser 3.1.0.' }
$contractOps = @([regex]::Matches($contractText, '(?m)^\s+operationId:\s*([A-Za-z_][A-Za-z0-9_]*)\s*$') | ForEach-Object { $_.Groups[1].Value })
if ($contractOps.Count -ne 21 -or (@($contractOps | Sort-Object -Unique)).Count -ne 21) { throw 'Contrato não tem 21 operationIds únicos.' }
$matrixOps = @($trace | ForEach-Object { $_.operationId })
if (@(Compare-Object ($contractOps | Sort-Object) ($matrixOps | Sort-Object)).Count -gt 0) { throw 'Divergência contrato vs matriz.' }
foreach ($row in $trace) {
    $matchStep = @($steps | Where-Object { $_.id -eq $row.step_id })
    if ($matchStep.Count -ne 1) { throw "Operação sem passo: $($row.operationId)" }
    if ($matchStep[0].operations -ne $row.operationId) { throw "Passo divergente: $($row.operationId)" }
    if ($matchStep[0].requires_http -ne '1') { throw "Endpoint sem gate HTTP: $($row.operationId)" }
}
Write-Host "MANIFEST PASS: $($steps.Count) passos; $($trace.Count) operationIds únicos; baseline OpenAPI 3.1.0." -ForegroundColor Green
