# ADR-0001 — Clean Architecture orientada por feature

- **Estado:** Premissas validadas em 19/10/2026; baseline OpenAPI v2 selecionado em BOOT-000
- **Decisores:** proprietário do FinTrack (aprovação de premissas nesta conversa)
- **Contexto:** Playbook v1 evoluiu numa solution simplificada em quatro projetos e já possui histórico de commits. Deseja-se uma segunda implementação didática rastreável, com independência de persistência e de interface HTTP.

## Decisão

Criar **novo repositório** para FinTrack v2. Manter cinco projetos: Api, Application, Domain, Infrastructure, Tests. Aplicar Clean Architecture/Dependency Inversion e Vertical Slices por feature. Handlers explícitos com DI nativa, **sem MediatR**. Um passo supervisionado por vez, testes/gates e um commit Git **somente após validação**. Preservar repositório v1 sem alterações. Preparar a Application para reutilização futura em aplicações Windows, **sem implementar Desktop agora**.

## Consequências

- Vantagens: domínio e casos de uso isolados; testes unitários independentes de HTTP e SQL; repositórios substituíveis; rastreabilidade e isolamento histórico.
- Custos: projeto Application e ports adicionais; arquitetura testável exige instrumentação e manutenção; os testes de banco real aumentam custo de CI.
- Restrições: OpenAPI como contrato externo, classes HTTP somente na Api; EF Core apenas em Infrastructure; sem nova camada genérica desnecessária.
- Riscos: o contrato 3.0.3 aprovado anteriormente não estava disponível para comparação direta; o OpenAPI 3.1.0 atual foi escolhido explicitamente como baseline canônico do v2, sem alegação de equivalência semântica. A política API-P01 permanece pendente antes de `updateCategory`.

## Alternativas rejeitadas

1. Manter todos os casos de uso em Api: acoplamento HTTP e difícil reuso Desktop.
2. Refatorar o repositório v1 em vez de recomeçar: perda de baseline separado e rastreabilidade desde o zero.
3. Adotar mediator/MediatR: custo didático sem exigência neste estágio.

## Verificação

`ArchitectureTests` (dependências de projeto, types e responsabilidades), contratos HTTP, testes de integração SQL Server e revisão humana por feature.
