# FinTrack v2 — Pacote de Playbook Codex (para revisão)

**NÃO é código de aplicação.** A solution e os cinco projetos serão criados posteriormente, somente quando `BOOT-001` for autorizado. A estrutura planejada é `src/FinTrack.Api`, `src/FinTrack.Application`, `src/FinTrack.Domain`, `src/FinTrack.Infrastructure` e `tests/FinTrack.Tests`. BOOT-000 é o único passo autorizado nesta execução; os demais aguardam autorização separada.

## Como revisar

1. Leia `docs/playbooks/FINTRACK_PLAYBOOK_V2.md` (guia principal e 44 fichas de execução).
2. Confira `docs/architecture/clean-architecture.md`, `docs/architecture/ADR-0001-clean-architecture-feature-first.md` e `docs/architecture/domain-and-requirements.md` e `docs/architecture/premises.md`.
3. Confira `docs/roadmap/steps.csv` (44 passos e flags de gates), `docs/traceability/endpoints.csv` (21 operações) e `docs/review/DECISIONS.md` (pendências explícitas).
4. O baseline canônico do v2 é OpenAPI 3.1.0 em `docs/contracts/fintrack-openapi.yaml`, selecionado explicitamente em BOOT-000. O contrato aprovado anteriormente em 3.0.3 não estava disponível para comparação; não se afirma equivalência semântica entre as versões.
5. Examine `AGENTS.md` e os scripts PowerShell **sem executá-los como implementação**.

## Como usar depois da aprovação do Playbook

- Crie um novo repositório Git `fintrack-clean-v2` (nome sugerido). Não altere o repositório antigo.
- Copie este pacote para a raiz. Inicialize o Git e configure a identidade conforme sua máquina.
- Autorize **somente** `BOOT-000` com prompt ao Codex. Ele deverá registrar e validar o baseline documental; `BOOT-001` somente sob autorização posterior.
- Ao construir código, cada passo cria relatório, executa `scripts/validate-step.ps1 -StepId CAT-004` (exemplo) e só então gera commit. Usar Windows PowerShell ou `pwsh`.
- Nenhum script executa commit ou push automaticamente. Gates de integração exigem SQL Server local via Docker quando a feature o introduzir.

## Estrutura do pacote

```text
AGENTS.md
README.md
.gitignore
docs/
  architecture/
    clean-architecture.md
    ADR-0001-clean-architecture-feature-first.md
    domain-and-requirements.md
    premises.md
  contracts/fintrack-openapi.yaml
  playbooks/FINTRACK_PLAYBOOK_V2.md
  roadmap/steps.csv
  traceability/endpoints.csv
  review/DECISIONS.md
  templates/{step-report.md,feature-acceptance.md}
scripts/{validate-step.ps1,check-architecture.ps1,verify-manifest.ps1}
```

**Critério central:** um passo supervisionado, um conjunto de gates reais, um único commit depois do sucesso, e revisão humana antes do passo seguinte (ou parada obrigatória conforme DEC-GOV-006 quando houver autorização em lote). Não afirmar 'testado' ou 'implementado' enquanto não existir execução real.
