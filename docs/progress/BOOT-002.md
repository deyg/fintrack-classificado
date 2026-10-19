# BOOT-002 — Instalar gates arquiteturais

- **Status:** VALIDADO (após gates e antes do commit)
- **Autorizado por:** autorização em lote DEC-GOV-006 ("siga com todos os passos")
- **Branch e base:** `master` sobre `e3eed77`
- **Feature:** 00 — Preparação
- **RF/RN/ADR:** ARQ-01; ARQ-04..08; ADR-0001
- **OpenAPI operationIds:** nenhum

## Objetivo e arquivos alterados

- `tests/FinTrack.Tests/FinTrack.Tests.csproj` — `NetArchTest.Rules` 1.3.2 (versão fixada).
- `tests/FinTrack.Tests/Architecture/LayerDependencyArchitectureTests.cs` — regras de dependência entre tipos e de referências de projeto.
- `tests/FinTrack.Tests/Architecture/ProjectReferenceRules.cs` — matriz de referências permitidas lida dos `.csproj`.
- `tests/FinTrack.Tests/Architecture/Fixtures/ForbiddenDependencyFixtures.cs` — tipos violadores para o teste negativo.

## Mudança implementada

Testes xUnit que garantem: Domain sem dependência de Api/Application/Infrastructure/frameworks; Application sem Api/Infrastructure/EF/ASP.NET/SqlClient; Infrastructure sem Api; Api sem Domain/EF/SqlClient; Api usa Infrastructure somente em `FinTrack.Api.Composition` ou `Program`; matriz de `ProjectReference`. Os testes negativos provam que violações simuladas são detectadas, sem alterar código de produção (nada precisa de rollback). Os assemblies de produção ainda são vazios; as regras ganham força conforme as features entram.

## Validação

| Gate | Comando | Resultado | Evidência |
|---|---|---|---|
| G2 | restore/build/test Release | PASS | 0 avisos, 0 erros, 10 testes |
| G3 | `check-architecture.ps1` + `ArchitectureTests` descobertos e executados | PASS | via validate-step |
| G9 | `verify-manifest.ps1` | PASS | 44 passos |

## Testes acrescentados/alterados

- Positivos: 6 regras de dependência + matriz de projetos.
- Negativos: `Dependency_rules_detect_simulated_violations`, `Project_reference_rule_detects_simulated_violations`.

## Bloqueios e decisões

- Nenhum.

## Resultado de revisão

- [x] Critérios de aceite satisfeitos e evidenciados
- [x] Diff inspecionado, sem segredos
- [x] Gates obrigatórios verdes
- [x] `git diff --cached --check` verde
- [x] Commit único
