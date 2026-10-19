# BOOT-000 — Congelar baseline documental v2

- **Status:** VALIDADO
- **Autorizado por:** autorização explícita: `Autorizo exclusivamente BOOT-000`
- **Data/hora:** 2026-10-19 16:18-03:00
- **Branch e base:** `master`; repositório novo, sem commit-base
- **Feature:** BOOT
- **RF/RN/ADR:** ARQ-01..08; PB-01..12; DEC-API-001
- **OpenAPI operationIds:** 21; baseline OpenAPI 3.1.0

## Objetivo e arquivos alterados

- `docs/review/DECISIONS.md` — registra DEC-API-001 como resolvida por escolha explícita do baseline 3.1.0 e preserva a limitação da comparação.
- `docs/playbooks/FINTRACK_PLAYBOOK_V2.md` — alinha autoridade, baseline e sequência autorizada.
- `README.md` — documenta o baseline escolhido e o estado do passo.
- `docs/architecture/ADR-0001-clean-architecture-feature-first.md` — atualiza status e risco contratual.
- `docs/architecture/domain-and-requirements.md` — atualiza a referência do contrato canônico.
- `docs/review/STATIC_VALIDATION.md` — adita o resultado da decisão sem alegar equivalência semântica.
- `docs/progress/BOOT-000.md` — registra escopo e evidências deste passo.

## Baseline congelado

- Produto: requisitos RF01–RF14 e regras RN01–RN12; nenhum código de aplicação foi criado.
- Arquitetura: cinco projetos planejados (`FinTrack.Api`, `FinTrack.Application`, `FinTrack.Domain`, `FinTrack.Infrastructure`, `FinTrack.Tests`), conforme as dependências documentadas.
- Contrato: OpenAPI 3.1.0 atual, com 21 operações, 25 schemas e 106 referências `$ref` resolvidas.
- Rastreabilidade: 44 passos e 21 operações mapeadas; `operationId`, método e rota conferidos entre YAML e CSV.
- Decisão DEC-API-001: resolvida por autorização explícita do usuário. O contrato 3.0.3 previamente aprovado não estava disponível neste workspace; sua equivalência semântica com 3.1.0 não foi verificada nem é alegada.
- Decisões restantes: API-P01 bloqueia CAT-007; DEC-RN-002, DEC-OPS-003, DEC-SDK-004 e DEC-ERR-005 continuam pendentes conforme seus passos.
- O repositório/histórico v1 não estava disponível; nenhuma alteração nele foi feita.

## Validação — resultados observados

| Gate | Comando/execução | Resultado PASS/FAIL/BLOCKED/N-A justificado | Evidência local |
|---|---|---|---|
| G0 - autorização | Revisão da autorização | PASS — exclusivamente BOOT-000 | Conversa atual |
| G0 - Git | `git rev-parse --is-inside-work-tree`; `git status --short --branch` | PASS — repositório novo inicializado nesta pasta; sem conteúdo preexistente divergente | `git init`; branch `master`; arquivos do pacote e relatório identificados |
| G1 - diff | `git diff --check`; `git diff --cached --check`; revisão de diff/segredos | PASS — após staging explícito e inspeção; nenhum segredo identificado | Executado antes do commit |
| G2 - build/test | Restore, build e testes .NET | N/A — BOOT-000 não cria solution/código | Não há `.sln`, `.csproj` ou fonte C# |
| G3 - arquitetura | Checker e ArchitectureTests | N/A — projetos e testes ainda não existem | Não há projetos C# |
| G4/G5 - SQL | Compose e IntegrationTests | N/A — passo documental, sem persistência | Não aplicável |
| G6 - OpenAPI | Parse YAML, contagem, referências e comparação com matriz | PASS — OpenAPI 3.1.0; 21 operações; 25 schemas; 106 `$ref` resolvidas; sem divergência de operationId/método/rota | Validação PyYAML registrada na execução do passo |
| G7 - cobertura | Relatório de cobertura | N/A — sem código/testes | Não aplicável |
| G9 - manifesto | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-manifest.ps1` | PASS — 44 passos e 21 operationIds únicos | Saída do verificador |
| Validador do passo | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-step.ps1 -StepId BOOT-000` | PASS — gates exigidos pelo manifesto concluídos | Saída do validador |

## Testes acrescentados/alterados

- Validação YAML e checagem cruzada de métodos, rotas e `operationId` contra `docs/traceability/endpoints.csv`.
- `verify-manifest.ps1` e `validate-step.ps1 -StepId BOOT-000`.
- Nenhum teste de aplicação; a solution será criada somente em BOOT-001.

## Bloqueios e decisões

- DEC-API-001 resolvida conforme aprovação explícita; contrato 3.0.3 não disponível, comparação direta não realizada.
- API-P01 continua bloqueando CAT-007; as demais decisões pendentes permanecem nos passos indicados em `docs/review/DECISIONS.md`.
- Sem bloqueio restante para BOOT-000.

## Resultado de revisão

- [x] Critérios documentais de BOOT-000 satisfeitos dentro dos artefatos disponíveis
- [x] Diff inspecionado, sem segredos ou mudanças fora do passo
- [x] Gates obrigatórios de BOOT-000 verdes
- [x] `git diff --cached --check` verde após staging explícito
- [x] Um commit atômico permitido após validação dos gates

> O SHA real será informado na resposta após o commit; não é registrado neste arquivo.
