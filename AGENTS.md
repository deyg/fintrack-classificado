# AGENTS.md — Codex, FinTrack v2.0

**Estado do trabalho:** Playbook proposto para revisão. As premissas foram aprovadas, mas **nenhum passo está autorizado por antecipação**, salvo pela autorização em lote registrada em DEC-GOV-006 (BOOT-002 a REL-005). Leia `docs/playbooks/FINTRACK_PLAYBOOK_V2.md` antes de agir.

## Comportamento obrigatório

1. Execute **apenas passos explicitamente autorizados** na conversa: `Autorizo exclusivamente <ID>` ou, conforme DEC-GOV-006, a autorização em lote `Autorizo BOOT-002 até REL-005, em ordem`. Um passo = um incremento pequeno e verificável = um commit após sucesso dos gates, mesmo dentro de um lote. Em lote, os passos seguem a ordem do roadmap, sem pular nem agrupar commits.
   - **Paradas obrigatórias do lote** (interromper sem commit do passo afetado e reportar): falha de gate ou ferramenta ausente; decisão pendente que bloqueia o passo (ex.: API-P01 em CAT-007, DEC-ERR-005, DEC-RN-002, DEC-OPS-003); necessidade de alterar contrato OpenAPI, regra ou arquitetura. Após resposta do usuário, o lote retoma do passo parado.
2. Antes de alterar qualquer arquivo, leia `docs/roadmap/steps.csv`, `docs/review/DECISIONS.md`, `docs/architecture/clean-architecture.md` e o OpenAPI para identificar escopo, pré-requisitos, decisões e contratos.
3. Implemente cada feature por fatias verticais. Use apenas cinco projetos: `FinTrack.Api`, `FinTrack.Application`, `FinTrack.Domain`, `FinTrack.Infrastructure`, `FinTrack.Tests`. Sem MediatR; handlers explícitos com DI nativa.
4. Dependências de compilação: `Api -> Application`, `Application -> Domain`, `Infrastructure -> Application + Domain` e `Api -> Infrastructure` **somente no Composition Root para DI**. `Domain` sem dependências entre projetos. Proibido `Api -> Domain`, `Application -> Infrastructure` e `Infrastructure -> Api`.
5. API é adapter HTTP, sem regras financeiras nem EF Core. Application executa casos de uso e declara ports. Domain guarda invariantes. Infrastructure implementa ports usando EF Core/SQL Server.
6. Não invente regras de negócio, endpoints, códigos HTTP ou schemas. Mudanças de contrato exigem impacto documentado e aprovação. **API-P01 (política de renomeação de categorias com histórico) permanece pendente e bloqueia CAT-007.**
7. Teste resultados positivos e negativos. Use SQL Server real isolado nos testes de persistência. Não trate EF InMemory como substituto de integração. Configure injeção do `TimeProvider` para datas de realização.
8. Prepare o relatório `docs/progress/<ID>.md`, rode `scripts/validate-step.ps1 -StepId <ID>`, inspecione `git diff --check`, `git diff`, `git diff --cached --check` e ausência de segredos. **Falha de gate => nenhum commit**.
9. Faça um único commit atômico, rastreável pelo ID do passo, após todos os gates verdes. Registre SHA verdadeiro só **após** o commit, no relatório enviado ao usuário; não tente embutir SHA do commit no próprio commit.
10. **Nunca avance** para além do passo ou do lote autorizado, nem faça push, merge, rebase destrutivo, PR, publicação ou release sem nova autorização. Não altere o repositório v1.
11. Se houver incompatibilidade ou ferramenta ausente, interrompa e reporte o bloqueio. Não declare sucesso sem execução real. Não descarte alterações preexistentes do usuário.

## Apresentação obrigatória após cada passo

`Passo | escopo | arquivos | RF/RN/operationId | comandos/gates e resultado | testes | SHA real | próximos riscos/pendências | próximo passo proposto (não iniciado)`.
