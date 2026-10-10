# Sefic API — clientes e fornecedores

API ASP.NET Core para gerir clientes e fornecedores

## Versões
- .NET SDK 10.0 (`net10.0`), ASP.NET Core 10, EF Core 10.0.12 (SQL Server)
- SQL Server 2022 (contentor Docker), xUnit 2.9.3

## Arquitetura (Clean Architecture, num único projeto)
Um só projeto (`Sefic.Api.csproj` na raiz, código em `src/`) com a separação por pastas/namespaces e dependências apenas para dentro
(`Api → Application → Domain`; `Infrastructure → Application/Domain`):
| Pasta | Responsabilidade |
|---|---|
| `src/Domain/` | Entidade `Partner`, `PartnerType`, regras do NIF (sem dependências de framework) |
| `src/Application/` | Casos de uso (`PartnerService`), validação, DTOs, porta `IPartnerRepository` |
| `src/Infrastructure/` | EF Core, mapeamento, migrações, repositório, seed |
| `src/Api/` | Controller e tratamento uniforme de erros (ProblemDetails) |
| `tests/Sefic.Tests` | Testes unitários e de integração contra SQL Server real |

Decisões-chave: unicidade do NIF por índice único (concorrência resolvida pela BD, erros 2601/2627 → 409);
`Version` = `rowversion` em Base64, comparada no `WHERE` do `UPDATE` (conflito → 409); filtros, contagem e
paginação executados em SQL; `Name` com colação `Latin1_General_100_CI_AS` (procura sem distinguir maiúsculas).

## Preparação
1. SQL Server a correr (exemplo):
   ```bash
   docker run -d --name sefic-sqlserver -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Your_password123' \
     -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
   ```
2. Connection string em `appsettings.json` (`ConnectionStrings:Default`, por defeito `localhost,14333`, sa/`Your_password123`).
3. Aplicar migrações:
   ```bash
   dotnet tool install --global dotnet-ef   # se necessário
   dotnet ef database update
   ```

## Execução
```bash
dotnet run      # http://localhost:5290
```
Em `Development` a API aplica as migrações e insere dados de exemplo (cadastro vazio) via `Seed:Enabled`.
Exemplos de pedidos em `sefic-api.http`.

## Docker
`Dockerfile` (multi-stage: SDK 10.0 para build, runtime `aspnet:10.0`, utilizador não-root) e
`docker-compose.yml` (API + SQL Server 2022 com volume persistente e healthcheck).

```bash
docker compose up -d --build     # sobe a BD, espera pelo healthcheck e arranca a API
curl "http://localhost:8080/api/partners"
docker compose logs -f api       # logs
docker compose down              # para (acrescente -v para apagar os dados)
```
- A API fica em `http://localhost:8080` e a BD em `localhost:14334` (evita colidir com um SQL Server local em 14333).
- No arranque a API aplica as migrações e insere os dados de exemplo (`Seed__Enabled=true`), pelo que não
  é preciso `dotnet ef database update`.
- Variáveis opcionais (ficheiro `.env` ou ambiente): `SA_PASSWORD` (por defeito `Your_password123`; tem de cumprir
  a política de passwords do SQL Server), `API_PORT` (8080), `SQL_PORT` (14334).
- Só a imagem da API: `docker build -t sefic-api .` e
  `docker run -p 8080:8080 -e ConnectionStrings__Default="<connection string>" sefic-api`.
- Os testes (`dotnet test`) correm no anfitrião contra qualquer SQL Server acessível, incluindo o do compose:
  `SEFIC_TEST_CONNECTION="Server=localhost,14334;User Id=sa;Password=Your_password123;TrustServerCertificate=True"`.

## Testes
```bash
dotnet test tests/Sefic.Tests
```
Cada classe de testes cria uma base `SeficTests_<guid>` própria e apaga-a no fim. Para outro servidor:
`SEFIC_TEST_CONNECTION="Server=...;User Id=...;Password=...;TrustServerCertificate=True"` (sem `Database`).
Os testes cobrem os cenários do enunciado, incluindo pedidos concorrentes (NIF duplicado e mesma `Version`).

## Endpoints
`POST /api/partners`, `GET /api/partners/{id}`, `GET /api/partners`, `PUT /api/partners/{id}`.
Erros: `400` (pedido/filtro/versão inválidos), `404`, `409` (NIF duplicado ou versão desatualizada),
sempre em `application/problem+json` (`status`, `title`, `detail`, `errors` opcional).
Notas: no `POST`, `CreditLimit` omitido assume 0; no `PUT` todos os campos são obrigatórios (`Email` pode ser `null`).
