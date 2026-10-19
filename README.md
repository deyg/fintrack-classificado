# FinTrack v2

API REST de finanças pessoais (categorias, contas, lançamentos e relatórios) em .NET 10, Clean Architecture por feature, EF Core e SQL Server. O contrato é [docs/contracts/fintrack-openapi.yaml](docs/contracts/fintrack-openapi.yaml) (OpenAPI 3.1.0, 21 operações sob `/api/v1`).

## O que o app faz

Registra o que você ganha e gasta e mostra relatórios sobre isso.

- **Categorias:** têm natureza de receita (`INCOME`) ou despesa (`EXPENSE`), por exemplo "Salário" ou "Moradia".
- **Contas:** pertencem a uma categoria, por exemplo "Aluguel" dentro de "Moradia". Contas de despesa recebem um classificador (`OF`, `OV`, `NOF` ou `NOV`). Uma conta inativa recusa novos lançamentos, mas o histórico continua consultável.
- **Lançamentos:** valores realizados em uma conta, com data (não pode ser futura) e descrição. Podem ser listados, filtrados, alterados e excluídos, inclusive por conta.
- **Relatórios:** dado um período (`from` e `to`), devolvem o resumo (receita, despesa e resultado) e os totais por categoria, por conta e por classificador. Os totais são calculados na hora, nunca guardados.

Regras de integridade: não se exclui categoria que tem contas, não se lança em conta inativa e não se muda a natureza de uma categoria que já tem contas. Erros seguem Problem Details (400, 404, 409, 422).

Não há autenticação, multiusuário nem interface: é somente a API.

## Pré-requisitos

- .NET SDK 10.0.112 (ver `global.json`; roll-forward só de patch)
- Docker (SQL Server 2022 via `compose.yaml`)
- Ferramenta `dotnet-ef` (`dotnet tool install --global dotnet-ef`)
- Windows PowerShell 5.1 ou `pwsh` para os scripts

## Primeira execução

1. Configure o segredo local (o `.env` é ignorado pelo Git; nunca o commite):
   ```powershell
   Copy-Item .env.example .env
   # edite .env: MSSQL_SA_PASSWORD (8+ caracteres, maiúscula, minúscula, número e símbolo) e, se quiser, MSSQL_PORT (padrão 14330)
   ```
2. Suba o SQL Server e aguarde ficar saudável:
   ```powershell
   docker compose up -d --wait sqlserver
   ```
3. Defina a connection string (variável de ambiente; sobrescreve o `appsettings.json`, que não tem segredo) e aplique as migrations. A API **não** migra o banco sozinha:
   ```powershell
   $cs = "Server=localhost,14330;Database=FinTrack;User Id=sa;Password=<MSSQL_SA_PASSWORD>;TrustServerCertificate=True"
   dotnet ef database update --project src/FinTrack.Infrastructure --startup-project src/FinTrack.Infrastructure --connection $cs
   ```
4. Rode a API (`http://localhost:8080` por padrão; em Development o OpenAPI sai em `/openapi/v1.json`):
   ```powershell
   $env:ConnectionStrings__FinTrack = $cs
   dotnet run --project src/FinTrack.Api -c Release
   ```
   Health check: `GET /health`.

### Alternativa: Prompt de Comando (cmd)

```bat
cd C:\28-fintrack-II
copy .env.example .env
docker compose up -d --wait sqlserver

set ConnectionStrings__FinTrack=Server=localhost,14330;Database=FinTrack;User Id=sa;Password=SUA_SENHA;TrustServerCertificate=True

dotnet ef database update --project src/FinTrack.Infrastructure --startup-project src/FinTrack.Infrastructure --connection "%ConnectionStrings__FinTrack%"

dotnet run --project src/FinTrack.Api -c Release
```

Troque `SUA_SENHA` pelo `MSSQL_SA_PASSWORD` do `.env` (edite o `.env` antes; pule o `copy` se ele já existir). O `dotnet ef` só é necessário na primeira vez ou após novas migrations. Se a senha tiver `&`, `%` ou `^`, use o PowerShell. Para parar: `Ctrl+C` e `docker compose down`.

## Testes e gates

```powershell
dotnet build FinTrack.sln -c Release
dotnet test FinTrack.sln -c Release                          # suíte completa; exige SQL Server do compose e .env
dotnet test FinTrack.sln -c Release --filter "FullyQualifiedName!~IntegrationTests&FullyQualifiedName!~ContractTests"   # sem banco
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/validate-step.ps1 -StepId <ID>   # gates de um passo do roadmap
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/measure-coverage.ps1             # cobertura (linhas e ramos), sem limite
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/smoke-roteiro.ps1                # roteiro das 21 operações (banco descartável)
```

Os testes de persistência e de contrato usam SQL Server real e isolado (um banco temporário por classe ou teste); EF InMemory não é usado como substituto.

## Roteiro das 21 operações

`scripts/smoke-roteiro.ps1` executa o roteiro abaixo ponta a ponta e confere os códigos HTTP (requer o SQL Server no ar, `.env` e `dotnet build -c Release`). Exemplos manuais (bash; `{id}` é o `id` devolvido pela criação):

```bash
B=http://localhost:8080/api/v1
curl -s -X POST $B/categories -H 'Content-Type: application/json' -d '{"name":"Moradia","nature":"EXPENSE"}'
curl -s -X POST $B/accounts -H 'Content-Type: application/json' -d '{"categoryId":1,"name":"Aluguel","classifier":"OF"}'
curl -s -X POST $B/transactions -H 'Content-Type: application/json' -d '{"accountId":1,"amount":1800.00,"realizedAt":"2026-10-05","description":"Aluguel"}'
curl -s "$B/reports/summary?from=2026-10-01&to=2026-10-31"
```

| Recurso | Operação | Método e caminho | Sucesso |
|---|---|---|---|
| Categorias | createCategory | `POST /categories` | 201 |
| | listCategories | `GET /categories` | 200 |
| | getCategory | `GET /categories/{id}` | 200 |
| | updateCategory | `PATCH /categories/{id}` | 200 |
| | deleteCategory | `DELETE /categories/{id}` | 204 |
| Contas | createAccount | `POST /accounts` | 201 |
| | listAccounts | `GET /accounts` | 200 |
| | getAccount | `GET /accounts/{id}` | 200 |
| | updateAccount | `PATCH /accounts/{id}` | 200 |
| | changeAccountStatus | `PATCH /accounts/{id}/status` (`{"active":false}`) | 200 |
| | deleteAccount | `DELETE /accounts/{id}` | 204 |
| Lançamentos | createTransaction | `POST /transactions` | 201 |
| | listTransactions | `GET /transactions` | 200 |
| | getTransaction | `GET /transactions/{id}` | 200 |
| | updateTransaction | `PATCH /transactions/{id}` | 200 |
| | deleteTransaction | `DELETE /transactions/{id}` | 204 |
| | listAccountTransactions | `GET /accounts/{id}/transactions` | 200 |
| Relatórios | getFinancialSummary | `GET /reports/summary?from=&to=` | 200 |
| | getReportByCategory | `GET /reports/by-category?from=&to=` | 200 |
| | getReportByAccount | `GET /reports/by-account?from=&to=` | 200 |
| | getReportByClassifier | `GET /reports/by-classifier?from=&to=` | 200 |

Erros usam `application/problem+json`: **400** sintaxe, tipo, propriedade desconhecida, corpo vazio ou parâmetro de rota/consulta inválido; **404** recurso inexistente; **409** conflito de regra (ex.: excluir categoria com contas, lançar em conta inativa); **422** valor inválido ou propriedade obrigatória ausente (DEC-ERR-005). Datas no formato `YYYY-MM-DD`.

## Estrutura

```text
src/FinTrack.Api            adapter HTTP (endpoints, Problem Details, Composition Root)
src/FinTrack.Application    casos de uso (handlers explícitos, sem MediatR), ports e DTOs
src/FinTrack.Domain         entidades e invariantes (sem dependências)
src/FinTrack.Infrastructure EF Core/SQL Server, repositórios e migrations
tests/FinTrack.Tests        unidade, arquitetura, persistência e contrato
docs/                       playbook, arquitetura, decisões, contrato OpenAPI, roadmap e relatórios por passo
scripts/                    gates (validate-step, check-architecture, verify-manifest), cobertura, smoke e probe SQL
```

Dependências: `Api → Application`, `Application → Domain`, `Infrastructure → Application + Domain`; `Api → Infrastructure` somente no Composition Root. Regras de negócio e decisões: [docs/architecture/](docs/architecture) e [docs/review/DECISIONS.md](docs/review/DECISIONS.md).

## Limitações conhecidas

- Sem autenticação/autorização e sem multiusuário.
- Sem concorrência otimista; verificações de regra (ex.: vínculo antes de excluir) não são serializadas e há janela de corrida.
- "Hoje" (limite de `realizedAt`) usa o fuso local do servidor; não há decisão formal sobre fuso.
- Migrations são aplicadas manualmente (`dotnet ef database update`).
- O CI (`.github/workflows/ci.yml`) está definido, mas ainda não foi executado no GitHub.
- Cobertura é apenas relatada (DEC-OPS-003); não há limite mínimo.
