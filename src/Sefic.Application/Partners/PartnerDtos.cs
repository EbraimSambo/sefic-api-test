namespace Sefic.Application.Partners;

public record CreatePartnerRequest(string? Name, string? TaxNumber, string? Type, string? Email, decimal? CreditLimit);

public record UpdatePartnerRequest(string? Name, string? TaxNumber, string? Type, string? Email,
    decimal? CreditLimit, bool? IsActive, string? Version);

public record ListPartnersRequest(string? Search, string? Type, bool IncludeInactive = false,
    int Page = 1, int PageSize = 20);

public record PartnerResponse(Guid Id, string Name, string TaxNumber, string Type, string? Email,
    decimal CreditLimit, bool IsActive, string Version, DateTime CreatedAt, DateTime UpdatedAt);
