# BOOT-003 — Inicializar API e padrão de erros

- **Status:** VALIDADO (após gates e antes do commit)
- **Autorizado por:** autorização em lote DEC-GOV-006
- **Branch e base:** `master` sobre `25825b6`
- **Feature:** 00 — Preparação
- **RF/RN/ADR:** RNF01; RNF05; RNF10
- **OpenAPI operationIds:** nenhum

## Objetivo e arquivos alterados

- `src/FinTrack.Api/Program.cs` — pipeline: exception handler, status code pages, OpenAPI (só Development), `/health`.
- `src/FinTrack.Api/Composition/DependencyInjection.cs` — Composition Root: Problem Details, handler global, OpenAPI, health checks.
- `src/FinTrack.Api/Errors/GlobalExceptionHandler.cs` — exceção → Problem Details (RFC 9457).
- `src/FinTrack.Api/appsettings.json` — `Urls` em `http://localhost:8080` (porta do contrato, configurável).
- `src/FinTrack.Api/FinTrack.Api.csproj` — `Microsoft.AspNetCore.OpenApi` 10.0.13.
- `tests/FinTrack.Tests/FinTrack.Tests.csproj` — `Microsoft.AspNetCore.Mvc.Testing` 10.0.13.
- `tests/FinTrack.Tests/Http/ApiBootstrapHttpTests.cs` — smoke HTTP e testes de middleware.

## Mudança implementada

Minimal API sem DbContext nem persistência. Erros seguem Problem Details com `type=https://httpstatuses.com/<status>`, `title`, `status`, `detail`, `instance`, `application/problem+json`. Exceção não tratada → 500 sem vazar mensagem ou tipo; `BadHttpRequestException` → 400 (sintaxe, conforme descrição `Error400` do contrato); rota inexistente → 404 Problem Details. `/health` fica fora de `/api/v1` e não é uma das 21 operações do contrato. O documento OpenAPI gerado em runtime (`/openapi/v1.json`, só Development) não substitui o contrato canônico em `docs/contracts`.

Fora de escopo: validação 400 vs 422 por campo (DEC-ERR-005, pendente para CAT-004), endpoints de negócio, persistência.

## Validação

| Gate | Comando | Resultado | Evidência |
|---|---|---|---|
| G2 | restore/build/test Release | PASS | 0 avisos, 0 erros, 15 testes |
| G3 | `check-architecture.ps1` + ArchitectureTests | PASS | via validate-step |
| G9 | `verify-manifest.ps1` | PASS | 44 passos |

## Testes acrescentados/alterados

- `Health_returns_200_without_persistence`; `Unknown_route_returns_problem_details_404`; `Unhandled_exception_returns_problem_details_500_without_leaking_internals`; `Malformed_request_returns_problem_details_400`; `OpenApi_document_is_exposed_only_in_Development`.
- Endpoints de falha existem somente via `IStartupFilter` dentro dos testes, nunca no código de produção.

## Bloqueios e decisões

- DEC-ERR-005 (400 vs 422 por validação) permanece pendente e bloqueia CAT-004; este passo não a decide.

## Resultado de revisão

- [x] Critérios de aceite satisfeitos e evidenciados
- [x] Diff inspecionado, sem segredos
- [x] Gates obrigatórios verdes
- [x] `git diff --cached --check` verde
- [x] Commit único
