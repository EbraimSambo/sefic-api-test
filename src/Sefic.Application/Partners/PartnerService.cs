using Sefic.Application.Common;
using Sefic.Domain.Partners;

namespace Sefic.Application.Partners;

public class PartnerService(IPartnerRepository repository, TimeProvider clock)
{
    public async Task<PartnerResponse> CreateAsync(CreatePartnerRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var data = PartnerValidator.Validate(request.Name, request.TaxNumber, request.Type, request.Email,
            request.CreditLimit, creditLimitRequired: false, errors);
        if (data is null) throw new ValidationException(errors);

        var partner = Partner.Create(data.Name, data.TaxNumber, data.Type, data.Email, data.CreditLimit,
            UtcNow());
        await repository.AddAsync(partner, ct);
        return ToResponse(partner);
    }

    public async Task<PartnerResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var partner = await repository.GetAsync(id, ct) ?? throw NotFound(id);
        return ToResponse(partner);
    }

    public async Task<PagedResult<PartnerResponse>> ListAsync(ListPartnersRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Page < 1) errors["Page"] = ["Page deve ser maior ou igual a 1."];
        if (request.PageSize is < 1 or > 100) errors["PageSize"] = ["PageSize deve estar entre 1 e 100."];

        PartnerType? type = null;
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (PartnerValidator.TryParseType(request.Type, out var parsed)) type = parsed;
            else errors["Type"] = ["Type deve ser CUSTOMER, SUPPLIER ou BOTH."];
        }
        if (errors.Count > 0) throw new ValidationException(errors);

        var search = request.Search?.Trim();
        string? name = null, tax = null;
        if (!string.IsNullOrEmpty(search))
        {
            name = search;
            var normalized = TaxNumberRules.StripSeparators(search);
            // Uma procura normalizada vazia não pode corresponder a todos os NIF.
            if (normalized.Length > 0) tax = normalized;
        }

        var result = await repository.SearchAsync(
            new PartnerFilter(name, tax, type, request.IncludeInactive, request.Page, request.PageSize), ct);
        return new PagedResult<PartnerResponse>(result.Items.Select(ToResponse).ToList(),
            result.TotalCount, result.Page, result.PageSize);
    }

    public async Task<PartnerResponse> UpdateAsync(Guid id, UpdatePartnerRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var data = PartnerValidator.Validate(request.Name, request.TaxNumber, request.Type, request.Email,
            request.CreditLimit, creditLimitRequired: true, errors);
        if (request.IsActive is null) errors["IsActive"] = ["IsActive é obrigatório."];
        var version = ParseVersion(request.Version, errors);
        if (errors.Count > 0 || data is null || version is null) throw new ValidationException(errors);

        var partner = await repository.GetForUpdateAsync(id, ct) ?? throw NotFound(id);
        partner.Update(data.Name, data.TaxNumber, data.Type, data.Email, data.CreditLimit,
            request.IsActive!.Value, UtcNow());
        await repository.SaveAsync(partner, version, ct);
        return ToResponse(partner);
    }

    // Truncado ao milissegundo (precisão da coluna datetime2(3)) para a resposta coincidir com o persistido.
    private DateTime UtcNow()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    private static byte[]? ParseVersion(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors["Version"] = ["Version é obrigatória."];
            return null;
        }
        var buffer = new byte[8];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != 8)
        {
            errors["Version"] = ["Version é inválida; use o valor devolvido pela API."];
            return null;
        }
        return buffer;
    }

    private static NotFoundException NotFound(Guid id) => new($"Não existe nenhuma entidade com o Id '{id}'.");

    private static PartnerResponse ToResponse(Partner p) => new(p.Id, p.Name, p.TaxNumber,
        PartnerValidator.TypeToString(p.Type), p.Email, p.CreditLimit, p.IsActive,
        Convert.ToBase64String(p.RowVersion), p.CreatedAt, p.UpdatedAt);
}
