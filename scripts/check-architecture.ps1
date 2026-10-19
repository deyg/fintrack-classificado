<#
  FinTrack v2 — verificação básica/automática de dependências de PROJETO
  e referências óbvias proibidas. Não substitui ArchitectureTests por
  análise de tipos/assemblies nem inspeção do Composition Root.

  Execução: powershell -NoProfile -File scripts/check-architecture.ps1
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projects = @{
    'FinTrack.Api' = 'src/FinTrack.Api/FinTrack.Api.csproj'
    'FinTrack.Application' = 'src/FinTrack.Application/FinTrack.Application.csproj'
    'FinTrack.Domain' = 'src/FinTrack.Domain/FinTrack.Domain.csproj'
    'FinTrack.Infrastructure' = 'src/FinTrack.Infrastructure/FinTrack.Infrastructure.csproj'
    'FinTrack.Tests' = 'tests/FinTrack.Tests/FinTrack.Tests.csproj'
}
$allowed = @{
    'FinTrack.Domain' = @()
    'FinTrack.Application' = @('FinTrack.Domain')
    'FinTrack.Infrastructure' = @('FinTrack.Application','FinTrack.Domain')
    'FinTrack.Api' = @('FinTrack.Application','FinTrack.Infrastructure')
    'FinTrack.Tests' = @('FinTrack.Api','FinTrack.Application','FinTrack.Domain','FinTrack.Infrastructure')
}
foreach ($name in $projects.Keys) {
    $projectPath = Join-Path $root $projects[$name]
    if (-not (Test-Path $projectPath)) { throw "Projeto ausente: $projectPath" }
    [xml]$xml = Get-Content -Raw -Path $projectPath -Encoding UTF8
    $refs = @($xml.SelectNodes('//*[local-name()="ProjectReference"]') | ForEach-Object {
        $refPath = $_.GetAttribute('Include')
        $last = @($refPath -split '[\\/]')[-1]
        [IO.Path]::GetFileNameWithoutExtension($last)
    })
    $unexpected = @($refs | Where-Object { $_ -notin $allowed[$name] })
    $missing = @($allowed[$name] | Where-Object { $_ -notin $refs })
    if ($unexpected.Count -gt 0) { throw "$name possui referência proibida: $($unexpected -join ', ')" }
    if ($missing.Count -gt 0) { throw "$name não contém referência arquitetural esperada: $($missing -join ', ')" }
}

function Assert-NoPattern($area, $expression, $message, [scriptblock] $skip = { $false }) {
    $folder = Join-Path $root $area
    if (-not (Test-Path $folder)) { throw "Área ausente: $folder" }
    $files = @(Get-ChildItem -Path $folder -Filter '*.cs' -File -Recurse | Where-Object { -not (& $skip $_) })
    foreach ($f in $files) {
        $matches = @(Select-String -Path $f.FullName -Pattern $expression)
        if ($matches.Count -gt 0) {
            throw "${message}: $($f.FullName):$($matches[0].LineNumber)"
        }
    }
}
Assert-NoPattern 'src/FinTrack.Domain' '(FinTrack\.(Api|Application|Infrastructure)|Microsoft\.(AspNetCore|EntityFrameworkCore)|FluentValidation|Microsoft\.Data\.SqlClient)' 'Domain acoplado a camada/framework proibido'
Assert-NoPattern 'src/FinTrack.Application' '(FinTrack\.(Api|Infrastructure)|Microsoft\.(AspNetCore|EntityFrameworkCore)|Microsoft\.Data\.SqlClient|\b(DbContext|DbSet|IQueryable|HttpContext|IResult)\b)' 'Application acoplada a transporte/persistência'
Assert-NoPattern 'src/FinTrack.Infrastructure' 'FinTrack\.Api' 'Infrastructure depende de Api'
Assert-NoPattern 'src/FinTrack.Api' '(Microsoft\.EntityFrameworkCore|Microsoft\.Data\.SqlClient|\b(DbContext|DbSet|IQueryable)\b|FinTrack\.Domain)' 'Api acessa domínio/persistência diretamente'
Assert-NoPattern 'src/FinTrack.Api' 'FinTrack\.Infrastructure' 'Api usa Infrastructure fora do Composition Root' {
    param($file)
    $rel = $file.FullName.Substring((Join-Path $root 'src/FinTrack.Api').Length).TrimStart([char]'/', [char]'\') -replace '\\','/'
    return $rel -eq 'Program.cs' -or $rel -eq 'Composition/DependencyInjection.cs'
}
Write-Host 'ARCH PROJECT/SCAN PASS: referências diretas verificadas; execute também ArchitectureTests reais.' -ForegroundColor Green
