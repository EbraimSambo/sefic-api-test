using Microsoft.AspNetCore.Mvc;
using Sefic.Application.Common;
using Sefic.Application.Partners;

namespace Sefic.Api.Controllers;

/// <summary>Gestão de clientes e fornecedores.</summary>
[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController(PartnerService service) : ControllerBase
{
    /// <summary>Cria uma entidade (cliente, fornecedor ou ambos).</summary>
    /// <remarks>
    /// O nome é aparado e o NIF normalizado (espaços, pontos e hífenes removidos; 9 dígitos).
    /// A entidade nasce ativa. Guarde o campo <c>version</c>: é necessário para atualizar.
    /// </remarks>
    /// <response code="201">Criada. O cabeçalho <c>Location</c> aponta para a entidade.</response>
    /// <response code="400">Dados inválidos (a resposta lista os erros por campo).</response>
    /// <response code="409">Já existe uma entidade com este NIF (mesmo que inativa).</response>
    [HttpPost]
    [ProducesResponseType<PartnerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create([FromBody] CreatePartnerRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Obtém uma entidade pelo Id, mesmo que esteja inativa.</summary>
    /// <param name="id">Identificador (GUID) da entidade.</param>
    /// <response code="200">Dados da entidade, incluindo a <c>version</c> atual.</response>
    /// <response code="400">O Id não é um GUID válido.</response>
    /// <response code="404">Não existe entidade com este Id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<PartnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PartnerResponse>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    /// <summary>Lista entidades com filtros e paginação.</summary>
    /// <remarks>
    /// Os filtros combinam-se com AND; ordenação por nome e depois Id.
    /// <c>type=CUSTOMER</c> inclui BOTH, <c>type=SUPPLIER</c> inclui BOTH e <c>type=BOTH</c> devolve apenas BOTH.
    /// </remarks>
    /// <param name="search">Ocorrência parcial no nome (sem distinguir maiúsculas) ou no NIF (separadores ignorados).</param>
    /// <param name="type">CUSTOMER, SUPPLIER ou BOTH.</param>
    /// <param name="includeInactive">Inclui entidades inativas (por defeito, não).</param>
    /// <param name="page">Página, a partir de 1.</param>
    /// <param name="pageSize">Itens por página, entre 1 e 100.</param>
    /// <response code="200">Página de resultados e total de registos antes da paginação.</response>
    /// <response code="400">Filtro ou paginação inválidos.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<PartnerResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PartnerResponse>>> List(
        [FromQuery] string? search, [FromQuery] string? type, [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.ListAsync(new ListPartnersRequest(search, type, includeInactive, page, pageSize), ct);

    /// <summary>Atualiza uma entidade; também desativa e reativa (<c>isActive</c>).</summary>
    /// <remarks>
    /// Todos os campos são obrigatórios (<c>email</c> pode ser <c>null</c>). A <c>version</c> tem de ser a última
    /// recebida: se o registo mudou entretanto, devolve 409 sem alterar nada.
    /// </remarks>
    /// <param name="id">Identificador (GUID) da entidade.</param>
    /// <response code="200">Atualizada; devolve os dados e a nova <c>version</c>.</response>
    /// <response code="400">Dados ou <c>version</c> inválidos.</response>
    /// <response code="404">Não existe entidade com este Id.</response>
    /// <response code="409">NIF de outra entidade ou <c>version</c> desatualizada.</response>
    [HttpPut("{id}")]
    [ProducesResponseType<PartnerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PartnerResponse>> Update(Guid id, [FromBody] UpdatePartnerRequest request,
        CancellationToken ct) => await service.UpdateAsync(id, request, ct);
}
