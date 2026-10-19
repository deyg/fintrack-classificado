<#
  FinTrack v2 — gates automáticos por ID (metadados: docs/roadmap/steps.csv)
  Exemplo: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-step.ps1 -StepId CAT-004

  Não faz git add/commit/push. O resultado PASS não dispensa revisão do diff,
  critérios semânticos da ficha e aceite humano.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(BOOT|CAT|ACC|TXN|REP|REL)-\d{3}$')]
    [string] $StepId
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Run([string] $tool, [string[]] $argv) {
    Write-Host ("`n> " + $tool + ' ' + ($argv -join ' ')) -ForegroundColor Cyan
    & $tool @argv
    if ($LASTEXITCODE -ne 0) { throw "Falha ($LASTEXITCODE): $tool $($argv -join ' ')" }
}
function Require-Tests([string] $pattern, [string] $name) {
    $folder = Join-Path $root 'tests/FinTrack.Tests'
    if (-not (Test-Path $folder)) { throw "Não há diretório de testes: $folder" }
    $files = @(Get-ChildItem $folder -Recurse -File -Filter $pattern)
    if ($files.Count -eq 0) { throw "Gate não implementado: arquivos de $name ($pattern) não encontrados." }
    # Evita sucesso falso se dotnet test filtrar e não encontrar testes.
    $output = & dotnet test FinTrack.sln -c Release --no-build --list-tests 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Falha ao descobrir testes ($name): $output" }
    if ($output -notmatch [regex]::Escape($name)) { throw "Nenhum teste $name descoberto pelo runner. Não considerar verde." }
    Run 'dotnet' @('test','FinTrack.sln','-c','Release','--no-build','--filter',"FullyQualifiedName~$name")
}

Push-Location $root
try {
    $row = @(Import-Csv 'docs/roadmap/steps.csv' | Where-Object { $_.id -eq $StepId })
    if ($row.Count -ne 1) { throw "Passo $StepId ausente/duplicado no manifesto." }
    $s = $row[0]
    if (-not (Test-Path "docs/progress/$StepId.md")) {
        throw "Falta evidência docs/progress/$StepId.md (crie a partir do template)."
    }
    Run 'git' @('rev-parse','--is-inside-work-tree')
    Run 'git' @('diff','--check')
    & (Join-Path $PSScriptRoot 'verify-manifest.ps1')
    if (-not $?) { throw 'Falha no manifesto.' }

    if ($s.requires_solution -eq '1') {
        if (-not (Test-Path 'FinTrack.sln')) { throw 'FinTrack.sln ausente. É obrigatório criar .sln (não somente .slnx).' }
        Run 'dotnet' @('restore','FinTrack.sln')
        Run 'dotnet' @('build','FinTrack.sln','-c','Release','--no-restore')
        Run 'dotnet' @('test','FinTrack.sln','-c','Release','--no-build')
    }
    if ($s.requires_arch -eq '1') {
        & (Join-Path $PSScriptRoot 'check-architecture.ps1')
        if (-not $?) { throw 'Falha no gate arquitetural estático.' }
        Require-Tests '*ArchitectureTests.cs' 'ArchitectureTests'
    }
    if ($s.requires_compose -eq '1') {
        if (-not (Test-Path 'compose.yaml')) { throw 'compose.yaml ausente.' }
        Run 'docker' @('compose','config','--quiet')
    }
    if ($s.requires_db -eq '1') {
        Run 'docker' @('compose','up','-d','--wait','sqlserver')
        Require-Tests '*IntegrationTests.cs' 'IntegrationTests'
    }
    if ($s.requires_http -eq '1') {
        Require-Tests '*ContractTests.cs' 'ContractTests'
    }
    if ($s.requires_coverage -eq '1') {
        $startTime = [DateTime]::UtcNow
        Run 'dotnet' @('test','FinTrack.sln','-c','Release','--no-build','--collect','XPlat Code Coverage')
        $coverageFiles = @(Get-ChildItem 'tests/FinTrack.Tests' -Recurse -File -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTimeUtc -ge $startTime.AddSeconds(-2) })
        if ($coverageFiles.Count -eq 0) { throw 'Nenhum XML Cobertura novo: o coletor pode não estar instalado.' }
        Write-Host "Cobertura gerada: $($coverageFiles[0].FullName). Meta numérica: validar decisão DEC-OPS-003." -ForegroundColor Yellow
    }
    Write-Host "`nGATES TÉCNICOS PASS: $StepId. Atenção: revisão de critérios, segredos e diff staged antes do commit." -ForegroundColor Green
}
finally { Pop-Location }
