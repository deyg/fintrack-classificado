# Registro de decisões para revisão — FinTrack v2

## Aprovado como premissa em 19/10/2026

- [x] Novo repositório v2, sem alterar repositório ou histórico v1.
- [x] Solution em **cinco projetos** — Api, Application, Domain, Infrastructure, Tests.
- [x] .NET 10, C# 14, Minimal APIs, EF Core + SQL Server, FluentValidation, xUnit, Docker Compose, OpenAPI 3.1, GitHub Actions.
- [x] Clean Architecture com testes das dependências e Application reutilizável fora de HTTP.
- [x] Desenvolvimento por features (Categorias, Contas, Lançamentos, Relatórios).
- [x] Handlers explícitos e DI nativa; não usar MediatR.
- [x] Codex opera **um passo por vez**; gates verdes e um commit por passo (autorização em lote permitida por DEC-GOV-006).
- [x] Possibilidade futura de FinTrack.Desktop sem incluí-lo no MVP.

## Decisão resolvida em BOOT-000

| ID | Decisão | Base e limite da evidência |
|---|---|---|
| DEC-API-001 | OpenAPI 3.1.0 em `docs/contracts/fintrack-openapi.yaml` é o baseline canônico do FinTrack v2, incluindo as 21 operações e 25 schemas existentes. | Aprovado explicitamente pelo usuário em 19/10/2026. O contrato 3.0.3 anteriormente aprovado não está disponível neste workspace; portanto, esta decisão seleciona o baseline v2, mas **não afirma compatibilidade nem equivalência semântica** com a versão 3.0.3. Nenhuma alteração de contrato foi feita neste passo. |

## Decisão de governança

| ID | Decisão | Limites |
|---|---|---|
| DEC-GOV-006 | Autorização única em lote, de BOOT-002 até REL-005, em ordem do roadmap. Aprovada pelo usuário em 19/10/2026. | Mantidos: um commit atômico por passo, gates verdes, relatório `docs/progress/<ID>.md`. Paradas obrigatórias: falha de gate/ferramenta, decisão pendente bloqueante (API-P01, DEC-ERR-005, DEC-RN-002, DEC-OPS-003), mudança de contrato/regra/arquitetura. Continuam proibidos sem nova autorização: push, merge, PR, release e alterar o repositório v1. |

## Deliberações pendentes (não tomar decisão automaticamente)

| ID | Questão | Impacto | Quando decidir |
|---|---|---|---|
| API-P01 | Política detalhada de renomeação de categorias que já possuem contas ou lançamentos históricos | `PATCH /categories/{id}` | **antes de CAT-007 (bloqueante)** |
| DEC-RN-002 | Validar efeitos de alteração de natureza de categoria associada a contas sem lançamentos e reclassificações de conta sem histórico; proposta conservadora = bloquear alterações que afetem associações/históricos | `PATCH categories` e `PATCH accounts` | antes de CAT-007/ACC-006 |
| DEC-OPS-003 | Especificar métricas e limite mínimo de cobertura após medição inicial; sem inventar limiar agora | gate de cobertura | REL-001 |
| DEC-SDK-004 | Selecionar o patch de SDK .NET 10 presente/instalável para `global.json`, com roll-forward dentro do limite acordado | reproducibilidade do build | BOOT-001 |
| DEC-ERR-005 | Confirmar distinção 400 vs 422 e payloads Problem Details conforme schema congelado | implementação de validators/erros | BOOT-003/CAT-004 |

## Como resolver

Registrar hipótese, alternativas, impactos em testes/OpenAPI/MER e autorização escrita; atualizar contrato e ADR com ID da decisão; validação de artefatos e commit dedicado somente no passo autorizado. Nenhuma divergência pode ser resolvida tacitamente pelo Codex. Decisões aprovadas não autorizam passos subsequentes.

**Estado dos passos:** BOOT-000 validado sob autorização exclusiva; os demais passos do v2 aguardam autorização separada.
