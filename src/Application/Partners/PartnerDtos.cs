namespace Sefic.Application.Partners;

/// <param name="Name">Obrigatório; 2 a 100 caracteres depois de Trim.</param>
/// <param name="TaxNumber">Obrigatório; 9 dígitos (espaços, pontos e hífenes são ignorados). Ex.: "001.234.567".</param>
/// <param name="Type">CUSTOMER, SUPPLIER ou BOTH.</param>
/// <param name="Email">Opcional; vazio é guardado como null.</param>
/// <param name="CreditLimit">Entre 0 e 1 000 000, máximo duas casas decimais. Por defeito, 0.</param>
public record CreatePartnerRequest(string? Name, string? TaxNumber, string? Type, string? Email, decimal? CreditLimit);

/// <param name="Name">Obrigatório; 2 a 100 caracteres depois de Trim.</param>
/// <param name="TaxNumber">Obrigatório; 9 dígitos (espaços, pontos e hífenes são ignorados).</param>
/// <param name="Type">CUSTOMER, SUPPLIER ou BOTH.</param>
/// <param name="Email">Pode ser null.</param>
/// <param name="CreditLimit">Obrigatório; entre 0 e 1 000 000, máximo duas casas decimais.</param>
/// <param name="IsActive">Obrigatório; false desativa, true reativa.</param>
/// <param name="Version">Obrigatória; o valor devolvido pela API na última leitura.</param>
public record UpdatePartnerRequest(string? Name, string? TaxNumber, string? Type, string? Email,
    decimal? CreditLimit, bool? IsActive, string? Version);

public record ListPartnersRequest(string? Search, string? Type, bool IncludeInactive = false,
    int Page = 1, int PageSize = 20);

/// <param name="Version">Token opaco (Base64) de concorrência otimista; envie-o no PUT.</param>
public record PartnerResponse(Guid Id, string Name, string TaxNumber, string Type, string? Email,
    decimal CreditLimit, bool IsActive, string Version, DateTime CreatedAt, DateTime UpdatedAt);
