# Sefic API — clientes e fornecedores

API REST em ASP.NET Core para gerir clientes e fornecedores de uma única empresa. Uma entidade pode ser
`CUSTOMER`, `SUPPLIER` ou `BOTH`, e existe no máximo um registo por NIF.

Destaques: NIF único garantido pela base de dados (também sob pedidos concorrentes), concorrência otimista com
`rowversion`, filtros e paginação executados no SQL Server, erros num formato único e testes de integração
contra SQL Server real.

## Índice
1. [Versões](#versões)
2. [Início rápido (Docker)](#início-rápido-docker)
3. [Execução local](#execução-local)
4. [Testes](#testes)
5. [Guia da API com exemplos](#guia-da-api-com-exemplos)
6. [Regras de validação](#regras-de-validação)
7. [Formato dos erros](#formato-dos-erros)
8. [Arquitetura e decisões](#arquitetura-e-decisões)
9. [Estrutura do repositório](#estrutura-do-repositório)
10. [Resolução de problemas](#resolução-de-problemas)

## Versões
| Componente | Versão |
|---|---|
| .NET / ASP.NET Core | 10.0 (`net10.0`) |
| EF Core + provider SQL Server | 10.0.12 |
| SQL Server | 2022 (imagem `mcr.microsoft.com/mssql/server:2022-latest`) |
| xUnit / Microsoft.AspNetCore.Mvc.Testing | 2.9.3 / 10.0.12 |
| dotnet-ef (ferramenta) | 10.x |

## Início rápido (Docker)
Requer apenas Docker. Sobe o SQL Server e a API, aplica as migrações e insere dados de exemplo:

```bash
docker compose up -d --build
curl "http://localhost:8080/api/partners"
```

| Serviço | Endereço |
|---|---|
| API | `http://localhost:8080` |
| SQL Server | `localhost:14334` (utilizador `sa`) |

Comandos úteis:
```bash
docker compose logs -f api     # logs da API
docker compose down            # para os contentores (mantém os dados)
docker compose down -v         # para e apaga também a base de dados
```
Variáveis opcionais (ambiente ou ficheiro `.env`): `SA_PASSWORD` (por defeito `Your_password123`; tem de cumprir a
política de passwords do SQL Server), `API_PORT` (8080), `SQL_PORT` (14334).

Só a imagem da API: `docker build -t sefic-api .` e
`docker run -p 8080:8080 -e ConnectionStrings__Default="<connection string>" sefic-api`.

> Nos exemplos abaixo usa-se `http://localhost:5290` (execução local). Com Docker, troque por `http://localhost:8080`.

## Execução local
Requisitos: .NET SDK 10 e um SQL Server acessível.

1. **SQL Server** (se ainda não tiver um):
   ```bash
   docker run -d --name sefic-sqlserver -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Your_password123' \
     -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest
   ```
2. **Connection string** em `appsettings.json` → `ConnectionStrings:Default`
   (por defeito `Server=localhost,14333;Database=SeficPartners;User Id=sa;Password=Your_password123;TrustServerCertificate=True`).
   Pode ser sobreposta pela variável de ambiente `ConnectionStrings__Default`.
3. **Migrações**:
   ```bash
   dotnet tool install --global dotnet-ef   # uma só vez
   dotnet ef database update
   ```
4. **Executar**:
   ```bash
   dotnet run                               # http://localhost:5290
   ```
   Em `Development` (perfil por defeito de `dotnet run`) a API aplica as migrações e insere 5 registos de exemplo
   se o cadastro estiver vazio (`Seed:Enabled=true`), pelo que o passo 3 é opcional.

Pedidos prontos a executar estão em [`sefic-api.http`](sefic-api.http) (VS Code com REST Client, Rider, Visual Studio).

Dados de exemplo inseridos:

| Name | TaxNumber | Type | CreditLimit |
|---|---|---|---|
| Ana Silva | 001234567 | CUSTOMER | 5000.00 |
| Bruno Costa Lda | 502345678 | SUPPLIER | 0.00 |
| Carla Mendes | 213456789 | BOTH | 12500.50 |
| Distribuições Dias SA | 504567890 | BOTH | 100000.00 |
| Eduardo Ferreira | 125678901 | CUSTOMER | 750.00 |

## Testes
```bash
dotnet test tests/Sefic.Tests
```
- São testes de integração contra **SQL Server real**: cada classe de testes cria uma base `SeficTests_<guid>`,
  aplica as migrações e apaga-a no fim. Não tocam na base de desenvolvimento.
- Servidor por defeito: `localhost,14333` (`sa` / `Your_password123`). Para outro servidor, defina
  `SEFIC_TEST_CONNECTION` **sem** `Database`:
  ```bash
  SEFIC_TEST_CONNECTION="Server=localhost,14334;User Id=sa;Password=Your_password123;TrustServerCertificate=True" \
    dotnet test tests/Sefic.Tests
  ```
- Cobertura (48 testes): os sete cenários do enunciado, incluindo **8 criações simultâneas com o mesmo NIF**
  (um 201 e sete 409) e **6 atualizações simultâneas com a mesma `Version`** (um 200 e cinco 409), feitas com
  pedidos/contextos independentes; validações 400; filtros combinados; `TotalCount` antes da paginação;
  desempate por Id; pesquisa que não faz corresponder todos os NIF; regras do NIF (unitários).

## Guia da API com exemplos
Todos os corpos são JSON (`Content-Type: application/json`). Os campos usam camelCase. Datas em UTC (ISO 8601 com `Z`).

| Método | Rota | Descrição | Sucesso |
|---|---|---|---|
| POST | `/api/partners` | Criar entidade | 201 + `Location` |
| GET | `/api/partners/{id}` | Obter por Id (mesmo se inativa) | 200 |
| GET | `/api/partners` | Listar com filtros e paginação | 200 |
| PUT | `/api/partners/{id}` | Atualizar, (des)ativar | 200 |

### 1. Criar — `POST /api/partners`
Campos: `name`, `taxNumber`, `type`, `email` (opcional), `creditLimit` (opcional, por defeito 0).
O nome é aparado e o NIF normalizado; a entidade nasce ativa.

```bash
curl -i -X POST http://localhost:5290/api/partners \
  -H "Content-Type: application/json" \
  -d '{"name":" Maria Souza ","taxNumber":"123.456.789","type":"BOTH","email":"maria@example.com","creditLimit":2500.50}'
```
```
HTTP/1.1 201 Created
Location: http://localhost:5290/api/partners/ac2355da-a861-40ae-a92c-7ba209185e8e

{"id":"ac2355da-a861-40ae-a92c-7ba209185e8e","name":"Maria Souza","taxNumber":"123456789","type":"BOTH",
 "email":"maria@example.com","creditLimit":2500.5,"isActive":true,"version":"AAAAAAAAB9Y=",
 "createdAt":"2026-10-11T14:25:20.119Z","updatedAt":"2026-10-11T14:25:20.119Z"}
```
Guarde o `version`: é necessário para atualizar.

**NIF duplicado** (qualquer representação do mesmo NIF, mesmo de uma entidade inativa) → `409`:
```bash
curl -X POST http://localhost:5290/api/partners -H "Content-Type: application/json" \
  -d '{"name":"Outra","taxNumber":"123 456 789","type":"SUPPLIER","creditLimit":1}'
```
```json
{"title":"Conflito","status":409,"detail":"Já existe uma entidade registada com este TaxNumber."}
```

**Dados inválidos** → `400` com todos os erros de uma vez:
```bash
curl -X POST http://localhost:5290/api/partners -H "Content-Type: application/json" \
  -d '{"name":"X Y","taxNumber":"12345678A","type":"X","creditLimit":10.999}'
```
```json
{"title":"Pedido inválido","status":400,"detail":"Um ou mais campos são inválidos.",
 "errors":{"TaxNumber":["TaxNumber é obrigatório e deve conter exatamente nove dígitos (espaços, pontos e hífenes são ignorados)."],
           "Type":["Type deve ser CUSTOMER, SUPPLIER ou BOTH."],
           "CreditLimit":["CreditLimit admite no máximo duas casas decimais."]}}
```

### 2. Obter por Id — `GET /api/partners/{id}`
```bash
curl http://localhost:5290/api/partners/ac2355da-a861-40ae-a92c-7ba209185e8e
```
Devolve os dados e a `version`, mesmo que a entidade esteja inativa. Id inexistente → `404`; Id que não é um GUID → `400`.
```json
{"title":"Não encontrado","status":404,"detail":"Não existe nenhuma entidade com o Id '00000000-0000-0000-0000-000000000000'."}
```

### 3. Listar — `GET /api/partners`
| Parâmetro | Por defeito | Descrição |
|---|---|---|
| `search` | — | Ocorrência parcial no nome (sem distinguir maiúsculas) **ou** no NIF (separadores `. - espaço` ignorados) |
| `type` | — | `CUSTOMER` (inclui BOTH), `SUPPLIER` (inclui BOTH), `BOTH` (apenas BOTH) |
| `includeInactive` | `false` | `true` inclui as inativas |
| `page` | `1` | Tem de ser ≥ 1 |
| `pageSize` | `20` | Entre 1 e 100 |

Os filtros combinam-se com AND; a ordenação é por `name` e depois `id`.
```bash
curl "http://localhost:5290/api/partners?search=ana&type=CUSTOMER&pageSize=5"
```
```json
{"items":[{"id":"c78fb072-0ba6-4340-9833-1f38394bd3b9","name":"Ana Silva","taxNumber":"001234567","type":"CUSTOMER",
           "email":"ana.silva@example.com","creditLimit":5000.00,"isActive":true,"version":"AAAAAAAAB9Q=",
           "createdAt":"2026-10-10T05:56:47.814Z","updatedAt":"2026-10-10T05:56:47.814Z"}],
 "totalCount":1,"page":1,"pageSize":5}
```
Mais exemplos:
```bash
curl "http://localhost:5290/api/partners?search=001.234"            # procura pelo NIF
curl "http://localhost:5290/api/partners?type=SUPPLIER"             # fornecedores (inclui BOTH)
curl "http://localhost:5290/api/partners?includeInactive=true"      # inclui inativas
curl "http://localhost:5290/api/partners?page=99"                   # além do fim: items [] e totalCount correto
curl "http://localhost:5290/api/partners?page=0"                    # 400
```
`totalCount` é calculado antes da paginação. Uma pesquisa só com separadores (ex.: `search=-.`) não corresponde a todos os NIF.

### 4. Atualizar e (des)ativar — `PUT /api/partners/{id}`
Exige **todos** os campos: `name`, `taxNumber`, `type`, `email` (pode ser `null`), `creditLimit`, `isActive` e `version`
(a última recebida). `id`, `createdAt` e `updatedAt` não são editáveis.

```bash
# desativar (o registo e o NIF continuam reservados)
curl -X PUT http://localhost:5290/api/partners/ac2355da-a861-40ae-a92c-7ba209185e8e \
  -H "Content-Type: application/json" \
  -d '{"name":"Maria Souza","taxNumber":"123456789","type":"CUSTOMER","email":null,"creditLimit":3000,"isActive":false,"version":"AAAAAAAAB9Y="}'
```
```json
{"id":"ac2355da-a861-40ae-a92c-7ba209185e8e","name":"Maria Souza","taxNumber":"123456789","type":"CUSTOMER","email":null,
 "creditLimit":3000,"isActive":false,"version":"AAAAAAAAB9g=","createdAt":"2026-10-11T14:25:20.119Z","updatedAt":"2026-10-11T14:25:25.078Z"}
```
Para reativar, envie `isActive: true` com a **nova** `version` (`AAAAAAAAB9g=`).

**Concorrência otimista.** Se alguém alterou o registo depois de o ler, a sua `version` está desatualizada:
```bash
# reutilizar a versão antiga (AAAAAAAAB9Y=) → 409, sem alterar nada
curl -X PUT http://localhost:5290/api/partners/ac2355da-a861-40ae-a92c-7ba209185e8e \
  -H "Content-Type: application/json" \
  -d '{"name":"Maria Souza","taxNumber":"123456789","type":"CUSTOMER","email":null,"creditLimit":3000,"isActive":true,"version":"AAAAAAAAB9Y="}'
```
```json
{"title":"Conflito","status":409,"detail":"A entidade foi alterada ou removida por outro pedido. Leia-a novamente e repita a atualização com a Version atual."}
```
Fluxo recomendado: `GET` → alterar → `PUT` com a `version` lida → se `409`, repetir o `GET` e reaplicar.
Uma `version` malformada → `400` (`"Version é inválida; use o valor devolvido pela API."`).

Mudar o NIF para o de **outra** entidade devolve `409` e mantém os dados anteriores; manter o próprio NIF é permitido.

## Regras de validação
| Campo | Regra |
|---|---|
| `name` | Obrigatório; 2 a 100 caracteres depois de `Trim` |
| `taxNumber` | Obrigatório; remove espaços, pontos e hífenes; exatamente 9 dígitos ASCII; guardado como texto (preserva zeros iniciais). Regra simplificada, sem validação fiscal real |
| `type` | `CUSTOMER`, `SUPPLIER` ou `BOTH` |
| `email` | Opcional; vazio ou só espaços → `null`; `Trim` e formato válido (máx. 254) |
| `creditLimit` | Entre 0 e 1 000 000, no máximo 2 casas decimais (`decimal(18,2)`); precisão a mais é **rejeitada**, nunca arredondada |
| `isActive` | `true` na criação; alterável apenas via `PUT` |
| `version` | Obrigatória no `PUT`; token opaco em Base64 devolvido pela API |

`createdAt` e `updatedAt` são definidos pelo servidor em UTC.

## Formato dos erros
Todos os erros são `application/problem+json` com `title`, `status`, `detail` e, nos erros de validação, `errors`
(campo → mensagens). Nunca expõem stack traces.

| Código | Quando |
|---|---|
| 400 | Validação, JSON malformado, `page`/`pageSize`/`type` inválidos, Id que não é GUID, `version` malformada |
| 404 | Id inexistente |
| 409 | NIF duplicado ou `version` desatualizada |
| 500 | Erro inesperado (mensagem genérica; detalhes só nos logs) |

## Arquitetura e decisões
Um só projeto, com separação por pastas e namespaces. As dependências apontam sempre para dentro
(`Api → Application → Domain`; `Infrastructure → Application/Domain`):

| Pasta | Responsabilidade |
|---|---|
| `src/Domain/` | Entidade `Partner`, `PartnerType`, regras do NIF. Sem dependências de framework |
| `src/Application/` | Casos de uso (`PartnerService`), validação, DTOs, porta `IPartnerRepository`, exceções (`Validation`, `NotFound`, `Conflict`) |
| `src/Infrastructure/` | EF Core, mapeamento, migrações, repositório, seed |
| `src/Api/` | Controller fino e tradução de exceções para HTTP |

**Porquê assim**
- **Uma só interface (`IPartnerRepository`).** Isola o EF Core: o serviço contém as regras e não conhece SQL, e traduz
  falhas da base de dados em exceções da aplicação (`ConflictException`). É a única abstração criada; não há
  interfaces "por reflexo".
- **Camadas por pasta e não por projeto.** Com quatro endpoints, vários projetos acrescentavam cerimónia sem
  benefício. A separação continua clara; o preço é que o compilador deixa de impedir dependências indevidas.
- **Controller fino.** Só recebe o pedido, chama o serviço e devolve o resultado. Erros são tratados num único
  `IExceptionHandler`, o que garante o formato consistente.

**Como cada requisito é garantido**
- **NIF único, mesmo com concorrência:** índice único em `TaxNumber`. Dois pedidos em simultâneo → o SQL Server aceita
  um e rejeita o outro (erro 2601/2627), traduzido em `409`. Não há locks em memória nem verificação prévia (que teria
  uma janela de corrida). O índice cobre também entidades inativas.
- **Concorrência otimista:** coluna `rowversion`, exposta em Base64. O `UPDATE` inclui `WHERE RowVersion = @versão`; se
  não atualizar nenhuma linha, o EF lança `DbUpdateConcurrencyException` → `409`. A comparação é feita pelo SQL Server,
  não em C#.
- **Atualização atómica:** cada `PUT` é um único `UPDATE`; uma validação falhada nunca chega à base de dados, logo não
  há alterações parciais.
- **Listagem:** filtros, `COUNT`, ordenação (`Name`, `Id`) e `OFFSET/FETCH` executados no SQL Server (`IQueryable`).
  A procura no nome usa `LIKE` com caracteres especiais escapados (`%` e `_` são literais) e a coluna `Name` tem
  colação `Latin1_General_100_CI_AS`, explícita, para ser insensível a maiúsculas.
- **Dados coerentes:** `CHECK` constraint em `CreditLimit`, `decimal(18,2)`, `datetime2(3)` em UTC; o valor devolvido
  pela API é o que foi persistido (datas truncadas ao milissegundo).

**Deliberadamente fora de âmbito:** CQRS/MediatR, AutoMapper, FluentValidation, repositório genérico/Unit of Work,
autenticação, frontend e serviços externos — não acrescentam valor a quatro endpoints e o enunciado não os pede.

## Estrutura do repositório
```
Sefic.Api.csproj           projeto da API (na raiz; bin/ e obj/ também)
appsettings*.json          configuração
Properties/                perfis de execução (porta 5290)
src/
  Domain/                  entidade e regras do NIF
  Application/             serviço, validação, DTOs, porta do repositório
  Infrastructure/          DbContext, mapeamento, repositório, migrações, seed
  Api/                     controller e tratamento de erros
  Program.cs               composição e arranque
tests/Sefic.Tests/         testes unitários e de integração
Dockerfile, docker-compose.yml, sefic-api.http, objective.md
```

## Resolução de problemas
| Sintoma | Causa provável / solução |
|---|---|
| `Login failed for user 'sa'` ou timeout ao ligar | SQL Server ainda a arrancar (aguarde ~20 s) ou password/porta diferentes; confirme a connection string |
| Password rejeitada pelo SQL Server | Tem de cumprir a política (8+ caracteres, maiúsculas, minúsculas, números/símbolos) |
| `Cannot open database "SeficPartners"` | Execute `dotnet ef database update` ou arranque em `Development` |
| Porta em uso | Altere `API_PORT`/`SQL_PORT` (Docker) ou `applicationUrl` em `Properties/launchSettings.json` |
| `dotnet ef` não encontrado | `dotnet tool install --global dotnet-ef` |
| Testes falham a ligar | Defina `SEFIC_TEST_CONNECTION` (sem `Database`) para um SQL Server acessível |
