#requires -Version 5.1
<#
.SYNOPSIS
  Mede a cobertura (Coverlet/Cobertura) e imprime linhas e ramos. Informativo: sem limite (DEC-OPS-003).
#>
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    $start = [DateTime]::UtcNow
    dotnet test FinTrack.sln -c Release --collect:'XPlat Code Coverage'
    if ($LASTEXITCODE -ne 0) { throw "Falha nos testes ($LASTEXITCODE)." }

    $file = Get-ChildItem 'tests/FinTrack.Tests' -Recurse -File -Filter 'coverage.cobertura.xml' |
        Where-Object { $_.LastWriteTimeUtc -ge $start.AddSeconds(-2) } |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (-not $file) { throw 'Nenhum XML Cobertura novo: o coletor pode não estar instalado.' }

    $c = ([xml](Get-Content $file.FullName)).coverage
    '{0}: linhas {1:P2} ({2}/{3}); ramos {4:P2} ({5}/{6})' -f $file.FullName,
        [double]$c.'line-rate', $c.'lines-covered', $c.'lines-valid',
        [double]$c.'branch-rate', $c.'branches-covered', $c.'branches-valid'
}
finally { Pop-Location }
