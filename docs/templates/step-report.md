# <STEP-ID> — <título>

- **Status:** EM EXECUÇÃO / BLOQUEADO / VALIDADO (após gates e antes do commit)
- **Autorizado por:** <referência à autorização explícita>
- **Data/hora:** <UTC + fuso local quando conhecido>
- **Branch e base:** <nome + SHA anterior ao passo>
- **Feature:** <feature>
- **RF/RN/ADR:** <IDs>
- **OpenAPI operationIds:** <IDs ou nenhum>

## Objetivo e arquivos alterados

- <arquivo> — <por quê>

## Mudança implementada

<Comportamento e o que ficou intencionalmente de fora.>

## Validação — registrar resultados reais

| Gate | Comando/execução | Resultado PASS/FAIL/BLOCKED/N-A justificado | Evidência local |
|---|---|---|---|
| G0 - autorização | revisão de prompt e dependências | | |
| G1 - Git | git diff --check, diff staged e revisão segredos | | |
| G2 - build/test | dotnet restore/build/test | | |
| G3 - arquitetura | check-architecture e ArchitectureTests | | |
| G4/G5 - SQL | docker compose + IntegrationTests | | |
| G6 - OpenAPI | ContractTests | | |
| G7 - cobertura | relatório Cobertura | | |
| G9 - manifesto | verify-manifest | | |

## Testes acrescentados/alterados

- Nome e casos positivos/negativos cobertos: ...

## Bloqueios e decisões

- Nenhum / DEC-... / API-P01 (com decisão e aprovação)

## Resultado de revisão

- [ ] Critérios de aceite satisfeitos e evidenciados
- [ ] Diff inspecionado, sem segredos ou mudanças fora do passo
- [ ] Gates obrigatórios verdes
- [ ] `git diff --cached --check` verde após `git add <paths>`
- [ ] Commit único permitido (somente após validar todos os itens)

> Não guardar aqui o SHA do **próprio commit**: este arquivo estará dentro do commit. Registrar o SHA real na resposta do Codex após `git rev-parse HEAD` ou em índice cronológico posterior (outro commit), evitando autorreferência.
