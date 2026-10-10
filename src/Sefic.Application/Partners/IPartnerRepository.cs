using Sefic.Application.Common;
using Sefic.Domain.Partners;

namespace Sefic.Application.Partners;

public record PartnerFilter(string? Name, string? TaxNumber, PartnerType? Type, bool IncludeInactive, int Page, int PageSize);

public interface IPartnerRepository
{
    /// <exception cref="ConflictException">NIF já existente.</exception>
    Task AddAsync(Partner partner, CancellationToken ct);

    /// <summary>Devolve a entidade para edição (acompanhada pela unidade de trabalho).</summary>
    Task<Partner?> GetForUpdateAsync(Guid id, CancellationToken ct);

    Task<Partner?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Grava as alterações exigindo que a versão na base de dados seja <paramref name="expectedVersion"/>.
    /// </summary>
    /// <exception cref="ConflictException">NIF duplicado ou versão desatualizada.</exception>
    Task SaveAsync(Partner partner, byte[] expectedVersion, CancellationToken ct);

    Task<PagedResult<Partner>> SearchAsync(PartnerFilter filter, CancellationToken ct);
}
