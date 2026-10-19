# FinTrack v2 — Premissas aprovadas (referência de IDs)

**Aprovação do usuário:** 19/10/2026. O aceite das premissas autoriza a **elaboração deste Playbook**, não a execução de um passo.

## Arquitetura

| ID | Premissa |
|---|---|
| ARQ-01 | Clean Architecture com dependências direcionadas ao núcleo. |
| ARQ-02 | Solution com cinco projetos: Api, Application, Domain, Infrastructure e Tests. |
| ARQ-03 | Organizar os casos de uso por feature (vertical slices). |
| ARQ-04 | Application independente de HTTP, banco e interface gráfica. |
| ARQ-05 | Domain não depende de Application, Infrastructure ou Api. |
| ARQ-06 | Interfaces de persistência/ports pertencem à Application. |
| ARQ-07 | Api referencia Infrastructure somente no Composition Root para DI. |
| ARQ-08 | Testes arquiteturais automatizados bloqueiam violações. |

## Tecnologia

| ID | Premissa |
|---|---|
| TEC-01 | .NET 10 LTS. |
| TEC-02 | C# 14. |
| TEC-03 | ASP.NET Core 10 Minimal APIs. |
| TEC-04 | Entity Framework Core 10. |
| TEC-05 | SQL Server. |
| TEC-06 | FluentValidation. |
| TEC-07 | OpenAPI 3.1. |
| TEC-08 | Swagger ou Scalar para documentação. |
| TEC-09 | xUnit. |
| TEC-10 | Testes arquiteturais com biblioteca de análise de dependências. |
| TEC-11 | Docker e Docker Compose. |
| TEC-12 | Git e GitHub. |
| TEC-13 | Codex no VS Code. |
| TEC-14 | GitHub Actions. |
| TEC-15 | Scripts PowerShell compatíveis com Windows. |

## Processo

| ID | Premissa |
|---|---|
| PB-01 | Feature-first: Categorias, Contas, Lançamentos, Relatórios. |
| PB-02 | Baby steps mínimos e verificáveis. |
| PB-03 | Autovalidação técnica proporcional por passo. |
| PB-04 | Dependências Clean Architecture são gates bloqueantes. |
| PB-05 | Um único commit Git por passo validado. |
| PB-06 | Rastreio Requisito → Caso de uso → Teste → Commit. |
| PB-07 | Registro de evidências verdadeiras por passo. |
| PB-08 | Codex não executa passos não autorizados. |
| PB-09 | Em falhas corrigir/revalidar ou interromper sem commit. |
| PB-10 | Verificar aderência contratual OpenAPI. |
| PB-11 | Testes reais de integração SQL Server. |
| PB-12 | Aprovação humana para avançar à próxima feature; aqui, também à próxima etapa individual. |

## Funcionalidades

| ID | Premissa |
|---|---|
| FUN-01 | MER Categoria–Conta–Lançamento. |
| FUN-02 | Categoria com `INCOME` ou `EXPENSE` no contrato externo. |
| FUN-03 | Classificador OF/OV/NOF/NOV pertence à Conta. |
| FUN-04 | Somente lançamentos efetivamente realizados. |
| FUN-05 | Preservação de histórico e integridade referencial. |
| FUN-06 | Exatamente 21 endpoints OpenAPI do MVP. |
| FUN-07 | Consolidações por conta, categoria, classificação e período. |
| FUN-08 | Orçamento mensal, autenticação e múltiplos usuários fora do MVP. |

## Decisões particulares aprovadas

| Tema | Escolha |
|---|---|
| Estratégia Git | Novo repositório v2, preservando anterior. |
| Autonomia do Codex | Um passo por vez, aguardando autorização. |
| Casos de uso | Handlers explícitos com DI nativa, sem MediatR. |
| Aplicação Windows | Application preparada para reutilização futura, sem Desktop no MVP. |
