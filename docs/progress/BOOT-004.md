# BOOT-004 — Infra SQL Server via Docker Compose

- **Status:** VALIDADO (após gates e antes do commit)
- **Autorizado por:** autorização em lote DEC-GOV-006
- **Branch e base:** `master` sobre `4ecb522`
- **Feature:** 00 — Preparação
- **RF/RN/ADR:** RNF02; RNF07
- **OpenAPI operationIds:** nenhum

## Objetivo e arquivos alterados

- `compose.yaml` — serviço `sqlserver` (imagem `mcr.microsoft.com/mssql/server:2022-latest`), healthcheck via `sqlcmd`, volume nomeado, porta `${MSSQL_PORT:-14330}`, senha obrigatória por `${MSSQL_SA_PASSWORD:?...}` sem valor padrão.
- `.env.example` — placeholders sem segredo; `.env` real fica ignorado pelo Git (já coberto por `.gitignore`).
- `scripts/probe-sqlserver.ps1` — probe `SELECT 1` dentro do container.
- `scripts/validate-step.ps1` — lacuna corrigida: passos `requires_compose` agora executam `docker compose up -d --wait sqlserver` e o probe (antes isso só ocorria com `requires_db`, deixando BOOT-004 sem o `up --wait` exigido pelo aceite). Sem `.env`/`MSSQL_SA_PASSWORD` o gate bloqueia com mensagem explícita.
- `src/FinTrack.Api/appsettings.json` — `ConnectionStrings:FinTrack` vazio; valor real vem de `ConnectionStrings__FinTrack` (variável de ambiente).
- `tests/FinTrack.Tests/Setup/ComposeConfigurationTests.cs` — testes isolados de configuração.

## Mudança implementada

Infraestrutura local de banco, sem EF Core nem acesso a dados na aplicação (CAT-003). A imagem usa a tag flutuante `2022-latest`; fixar um CU específico é refinamento futuro.

## Validação

| Gate | Comando | Resultado | Evidência |
|---|---|---|---|
| G2 | restore/build/test Release | PASS | 0 avisos, 0 erros, 21 testes |
| G3 | check-architecture + ArchitectureTests | PASS | via validate-step |
| G4 | `docker compose config`, `up -d --wait sqlserver`, probe | PASS | container saudável, `SELECT 1` = 1 |
| G9 | verify-manifest | PASS | 44 passos |

## Testes acrescentados/alterados

- `ComposeConfigurationTests`: compose com healthcheck e senha por ambiente; `.env.example` sem valor; `.gitignore` protege `.env`; appsettings sem segredo; variável de ambiente sobrepõe appsettings; connection string vazia no repositório.

## Bloqueios e decisões

- Nenhum. O `.env` local (ignorado) contém a senha de desenvolvimento e não entra no commit.

## Resultado de revisão

- [x] Critérios de aceite satisfeitos e evidenciados
- [x] Diff inspecionado, sem segredos (`.env` ignorado)
- [x] Gates obrigatórios verdes
- [x] `git diff --cached --check` verde
- [x] Commit único
