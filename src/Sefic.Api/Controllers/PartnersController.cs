using Microsoft.AspNetCore.Mvc;
using Sefic.Application.Common;
using Sefic.Application.Partners;

namespace Sefic.Api.Controllers;

[ApiController]
[Route("api/partners")]
public class PartnersController(PartnerService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<PartnerResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePartnerRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PartnerResponse>> Get(Guid id, CancellationToken ct) =>
        await service.GetAsync(id, ct);

    [HttpGet]
    public async Task<ActionResult<PagedResult<PartnerResponse>>> List(
        [FromQuery] string? search, [FromQuery] string? type, [FromQuery] bool includeInactive = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        await service.ListAsync(new ListPartnersRequest(search, type, includeInactive, page, pageSize), ct);

    [HttpPut("{id}")]
    public async Task<ActionResult<PartnerResponse>> Update(Guid id, [FromBody] UpdatePartnerRequest request,
        CancellationToken ct) => await service.UpdateAsync(id, request, ct);
}
