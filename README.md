# Sefic API — clientes e fornecedores

API ASP.NET Core para gerir clientes e fornecedores (NIF único, concorrência otimista).

## Versões
- .NET SDK 10.0 (`net10.0`), ASP.NET Core 10, EF Core 10.0.12 (SQL Server)
- SQL Server 2022 (contentor Docker), xUnit 2.9.3

## Arquitetura (Clean Architecture)
| Projeto | Responsabilidade |
|---|---|
| `src/Sefic.Domain` | Entidade `Partner`, `PartnerType`, regras do NIF (sem dependências) |
| `src/Sefic.Application` | Casos de uso (`PartnerService`), validação, DTOs, porta `IPartnerRepository` |
| `src/Sefic.Infrastructure` | EF Core, mapeamento, migrações, repositório, seed |
| `src/Sefic.Api` | Controller, tratamento uniforme de erros (ProblemDetails) |
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
2. Connection string em `src/Sefic.Api/appsettings.json` (`ConnectionStrings:Default`, por defeito `localhost,14333`, sa/`Your_password123`).
3. Aplicar migrações:
   ```bash
   dotnet tool install --global dotnet-ef   # se necessário
   dotnet ef database update -p src/Sefic.Infrastructure
   ```

## Execução
```bash
dotnet run --project src/Sefic.Api      # http://localhost:5290
```
Em `Development` a API aplica as migrações e insere dados de exemplo (cadastro vazio) via `Seed:Enabled`.
Exemplos de pedidos em `sefic-api.http`.

## Testes
```bash
dotnet test
```
Cada classe de testes cria uma base `SeficTests_<guid>` própria e apaga-a no fim. Para outro servidor:
`SEFIC_TEST_CONNECTION="Server=...;User Id=...;Password=...;TrustServerCertificate=True"` (sem `Database`).
Os testes cobrem os cenários do enunciado, incluindo pedidos concorrentes (NIF duplicado e mesma `Version`).

## Endpoints
`POST /api/partners`, `GET /api/partners/{id}`, `GET /api/partners`, `PUT /api/partners/{id}`.
Erros: `400` (pedido/filtro/versão inválidos), `404`, `409` (NIF duplicado ou versão desatualizada),
sempre em `application/problem+json` (`status`, `title`, `detail`, `errors` opcional).
Notas: no `POST`, `CreditLimit` omitido assume 0; no `PUT` todos os campos são obrigatórios (`Email` pode ser `null`).
