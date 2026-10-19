# BOOT-005 — Estabelecer CI e contrato de gates

- **Status:** VALIDADO localmente (execução remota NÃO realizada)
- **Autorizado por:** autorização em lote DEC-GOV-006
- **Branch e base:** `master` sobre `c802402`
- **Feature:** 00 — Preparação
- **RF/RN/ADR:** PB-03..10; TEC-14..15
- **OpenAPI operationIds:** nenhum

## Objetivo e arquivos alterados

- `.github/workflows/ci.yml` — jobs `build-test` (ubuntu e windows, SDK de `global.json`: manifesto, check-architecture, restore, build, test com ArchitectureTests, upload de TRX) e `sql-server` (`.env` efêmero com senha aleatória mascarada, `compose config`, `up --wait`, probe, `down -v`).
- `tests/FinTrack.Tests/Setup/CiWorkflowTests.cs` — lint do workflow: matriz de SO, SDK fixado, gates presentes, sem `continue-on-error`/`|| true`/`secrets.`, permissões mínimas.

## Mudança implementada

CI que executa somente gates já existentes e não mascara etapas indisponíveis. Fora de escopo e não existentes ainda: cobertura (REL-001), ContractTests (CAT-004), IntegrationTests (CAT-003); entram no workflow nos passos que os criam. Actions usadas por tag principal (`@v4`); fixação por SHA é um endurecimento opcional.

## Validação

| Gate | Comando | Resultado | Evidência |
|---|---|---|---|
| G2 | restore/build/test Release | PASS | 0 avisos, 0 erros, 24 testes |
| G3 | check-architecture + ArchitectureTests | PASS | via validate-step |
| YAML | PyYAML `safe_load` | PASS | jobs `build-test`, `sql-server`; gatilhos push/pull_request |
| G9 | verify-manifest | PASS | 44 passos |

**Não verificado:** execução real no GitHub Actions (não há remoto nem push autorizado) e execução dos scripts sob `pwsh` em Linux (pwsh ausente nesta máquina). **"CI bloqueia merges inválidos" depende de proteção de branch** (status checks `build-test` e `sql-server` obrigatórios) configurada no GitHub pelo usuário; não é automatizável por este repositório.

## Testes acrescentados/alterados

- `CiWorkflowTests` (3 testes).

## Bloqueios e decisões

- Nenhum. Pendência de configuração manual: branch protection após o primeiro push.

## Resultado de revisão

- [x] Critérios de aceite satisfeitos (com a ressalva de execução remota acima)
- [x] Diff inspecionado, sem segredos
- [x] Gates obrigatórios verdes
- [x] `git diff --cached --check` verde
- [x] Commit único
