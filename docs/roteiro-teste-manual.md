# Roteiro de teste manual

Comandos para Windows PowerShell 5.1. Executado e validado contra a API real (ver "Resultado esperado").

## 1. Subir a aplicação

```powershell
cd C:\28-fintrack-II
if (-not (Test-Path .env)) { Copy-Item .env.example .env }   # edite MSSQL_SA_PASSWORD se quiser
docker compose up -d --wait sqlserver

# a senha vem do .env
$pw = ((Get-Content .env | Select-String '^MSSQL_SA_PASSWORD=').Line -split '=',2)[1]
$env:ConnectionStrings__FinTrack = "Server=localhost,14330;Database=FinTrack;User Id=sa;Password=$pw;TrustServerCertificate=True"

# a API não migra sozinha: aplique as migrations uma vez
dotnet ef database update --project src/FinTrack.Infrastructure --startup-project src/FinTrack.Infrastructure --connection $env:ConnectionStrings__FinTrack

dotnet run --project src/FinTrack.Api -c Release     # http://localhost:8080
```

Se mudou `MSSQL_PORT` no `.env`, troque `14330` na connection string. Deixe esse terminal aberto e rode o resto em outro. Verificação:

```powershell
curl.exe -s http://localhost:8080/health
```

## 2. Sequência de curls

No PowerShell 5.1, `curl` é alias de outro comando e o `-d` com JSON contendo espaços é quebrado em vários argumentos. Por isso o helper `C` abaixo usa `curl.exe` e envia o corpo por arquivo temporário. Ele guarda o último retorno para capturar os ids (o banco pode já ter dados, então não assuma ids 1 e 2).

```powershell
$B = "http://localhost:8080/api/v1"
function C($m,$u,$d){
  $a=@('-s','-w',' [%{http_code}]','-X',$m,"$B$u")
  if($d){ $f=[IO.Path]::GetTempFileName(); [IO.File]::WriteAllText($f,$d,(New-Object Text.UTF8Encoding($false)))
          $a+=@('-H','Content-Type: application/json','--data-binary',"@$f") }
  $o=(& curl.exe @a) -join ''; Write-Host "$m $u -> $o"; $script:last=$o
}
function Id { ([regex]::Match($script:last,'"id":(\d+)')).Groups[1].Value }

# Categorias
C POST /categories '{"name":"Salario","nature":"INCOME"}';  $c1 = Id
C POST /categories '{"name":"Moradia","nature":"EXPENSE"}'; $c2 = Id
C GET /categories
C GET /categories/$c1
C PATCH /categories/$c2 '{"name":"Moradia e contas"}'

# Contas (receita sem classificador; despesa com OF, OV, NOF ou NOV)
C POST /accounts ('{"categoryId":'+$c1+',"name":"Salario mensal"}'); $a1 = Id
C POST /accounts ('{"categoryId":'+$c2+',"name":"Aluguel","classifier":"OF"}'); $a2 = Id
C GET /accounts
C GET /accounts/$a2
C PATCH /accounts/$a2 '{"name":"Aluguel apto"}'

# Lançamentos (realizedAt não pode ser futuro)
C POST /transactions ('{"accountId":'+$a1+',"amount":5000.00,"realizedAt":"2026-10-05","description":"Salario outubro"}'); $t1 = Id
C POST /transactions ('{"accountId":'+$a2+',"amount":1800.00,"realizedAt":"2026-10-10","description":"Aluguel outubro"}'); $t2 = Id
C GET /transactions
C GET /transactions/$t2
C PATCH /transactions/$t2 '{"amount":1850.00}'
C GET /accounts/$a2/transactions

# Relatórios (from e to são obrigatórios)
C GET "/reports/summary?from=2026-10-01&to=2026-10-31"
C GET "/reports/by-category?from=2026-10-01&to=2026-10-31"
C GET "/reports/by-account?from=2026-10-01&to=2026-10-31"
C GET "/reports/by-classifier?from=2026-10-01&to=2026-10-31"
```

Resultado esperado do resumo: `income 5000.00`, `expenses 1850.00`, `balance 3150.00`.

## 3. Casos de erro

```powershell
C DELETE /categories/$c2                                    # 409: categoria com contas
C PATCH /accounts/$a2/status '{"active":false}'             # 200
C POST /transactions ('{"accountId":'+$a2+',"amount":10,"realizedAt":"2026-10-11","description":"x"}')   # 409: conta inativa
C PATCH /accounts/$a2/status '{"active":true}'              # 200
C GET /categories/999999                                    # 404
C POST /transactions ('{"accountId":'+$a2+',"amount":10,"realizedAt":"2030-01-01","description":"x"}')   # 422: data futura
C GET /reports/summary                                      # 400: from/to ausentes
```

## 4. Limpeza (a ordem importa)

```powershell
foreach($u in "/transactions/$t1","/transactions/$t2","/accounts/$a1","/accounts/$a2","/categories/$c1","/categories/$c2"){ C DELETE $u }   # cada um deve devolver [204]
```

Para parar: `Ctrl+C` no terminal da API e `docker compose down`.

Alternativa automática: `scripts\smoke-roteiro.ps1` (banco descartável, confere os códigos HTTP).
