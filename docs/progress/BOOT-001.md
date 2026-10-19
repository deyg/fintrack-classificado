# BOOT-001 — Criar solution e SDK reprodutível

- **Status:** VALIDADO (após gates e antes do commit)
- **Autorizado por:** "Autorizo exclusivamente BOOT-001"; DEC-SDK-004 = fixar 10.0.112 com `rollForward: latestPatch`.
- **Branch e base:** `master` sobre `12e11d6`
- **Feature:** 00 — Preparação
- **RF/RN/ADR:** ARQ-02; TEC-01..02; ADR-0001
- **OpenAPI operationIds:** nenhum

## Objetivo e arquivos alterados

- `global.json` — SDK 10.0.112, `latestPatch` (DEC-SDK-004).
- `Directory.Build.props` — net10.0, C# 14, nullable, warnings como erro.
- `FinTrack.sln` — cinco projetos.
- `src/FinTrack.{Domain,Application,Infrastructure,Api}` — projetos com referências controladas e `AssemblyMarker`; `Program.cs` vazio (sem endpoints; BOOT-003 trata a API).
- `tests/FinTrack.Tests` — xUnit com smoke tests.
- `scripts/check-architecture.ps1` — corrigido erro de parse (`$x:` interpretado como drive) que impedia o gate de executar.

## Mudança implementada

Esqueleto compilável da Clean Architecture. Referências: Api→Application+Infrastructure; Application→Domain; Infrastructure→Application+Domain; Domain sem referências; Tests→4 projetos. Sem lógica, sem MediatR, sem pacotes além do xUnit/coverlet. Fora de escopo: ArchitectureTests (BOOT-002), API (BOOT-003).

## Validação — registrar resultados reais

| Gate | Comando/execução | Resultado | Evidência local |
|---|---|---|---|
| G0 - autorização | autorização explícita BOOT-001 | PASS | conversa |
| G1 - Git | `git diff --check`, staged, revisão de segredos | PASS | ver resposta |
| G2 - build/test | `dotnet restore/build -c Release/test -c Release` | PASS | 0 avisos, 0 erros, 2 testes aprovados |
| G3 - arquitetura | `scripts/check-architecture.ps1` | PASS | referências/scan |
| G4/G5 - SQL | — | N-A | `requires_db=0` |
| G6 - OpenAPI | — | N-A | sem HTTP |
| G7 - cobertura | — | N-A | REL-001 |
| G9 - manifesto | `verify-manifest.ps1` | PASS | 44 passos, 21 operationIds |

`dotnet --list-sdks`: `9.0.300`, `10.0.112` (ambos em `C:\Program Files\dotnet\sdk`); `global.json` fixa 10.0.112.

## Testes acrescentados/alterados

- `BootstrapSmokeTests.Solution_assemblies_are_loadable`: os quatro assemblies carregam com os nomes esperados.
- `BootstrapSmokeTests.Runtime_targets_net10`: runtime major 10.

## Bloqueios e decisões

- DEC-SDK-004 resolvida: 10.0.112 + `latestPatch`.
- Correção do parse error em `check-architecture.ps1` (preexistente, acoplado ao gate de referências).

## Resultado de revisão

- [x] Critérios de aceite satisfeitos e evidenciados
- [x] Diff inspecionado, sem segredos ou mudanças fora do passo
- [x] Gates obrigatórios verdes
- [x] `git diff --cached --check` verde após `git add <paths>`
- [x] Commit único permitido
