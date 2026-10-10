using System.Net.Mail;
using Sefic.Domain.Partners;

namespace Sefic.Application.Partners;

public record ValidPartnerData(string Name, string TaxNumber, PartnerType Type, string? Email, decimal CreditLimit);

internal static class PartnerValidator
{
    public static bool TryParseType(string? value, out PartnerType type)
    {
        type = default;
        return value?.Trim().ToUpperInvariant() switch
        {
            "CUSTOMER" => Set(PartnerType.Customer, out type),
            "SUPPLIER" => Set(PartnerType.Supplier, out type),
            "BOTH" => Set(PartnerType.Both, out type),
            _ => false
        };
    }

    private static bool Set(PartnerType value, out PartnerType type) { type = value; return true; }

    public static string TypeToString(PartnerType type) => type switch
    {
        PartnerType.Customer => "CUSTOMER",
        PartnerType.Supplier => "SUPPLIER",
        _ => "BOTH"
    };

    public static ValidPartnerData? Validate(string? name, string? taxNumber, string? type, string? email,
        decimal? creditLimit, bool creditLimitRequired, Dictionary<string, string[]> errors)
    {
        var okName = true;
        var cleanName = name?.Trim() ?? string.Empty;
        if (cleanName.Length is < Partner.NameMinLength or > Partner.NameMaxLength)
        {
            errors["Name"] = [$"Name é obrigatório e deve ter entre {Partner.NameMinLength} e {Partner.NameMaxLength} caracteres."];
            okName = false;
        }

        var okTax = TaxNumberRules.TryNormalize(taxNumber, out var normalizedTax);
        if (!okTax)
            errors["TaxNumber"] = ["TaxNumber é obrigatório e deve conter exatamente nove dígitos (espaços, pontos e hífenes são ignorados)."];

        var okType = TryParseType(type, out var partnerType);
        if (!okType)
            errors["Type"] = ["Type deve ser CUSTOMER, SUPPLIER ou BOTH."];

        string? cleanEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        var okEmail = cleanEmail is null || IsValidEmail(cleanEmail);
        if (!okEmail)
            errors["Email"] = ["Email tem um formato inválido."];

        var limit = creditLimit ?? 0m;
        var okLimit = true;
        if (creditLimit is null && creditLimitRequired)
        {
            errors["CreditLimit"] = ["CreditLimit é obrigatório."];
            okLimit = false;
        }
        else if (limit < 0 || limit > Partner.CreditLimitMax)
        {
            errors["CreditLimit"] = [$"CreditLimit deve estar entre 0 e {Partner.CreditLimitMax:0}."];
            okLimit = false;
        }
        else if (decimal.Round(limit, 2) != limit)
        {
            errors["CreditLimit"] = ["CreditLimit admite no máximo duas casas decimais."];
            okLimit = false;
        }

        return okName && okTax && okType && okEmail && okLimit
            ? new ValidPartnerData(cleanName, normalizedTax, partnerType, cleanEmail, decimal.Round(limit, 2))
            : null;
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length > Partner.EmailMaxLength || email.Any(char.IsWhiteSpace)) return false;
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email) return false;
        return address.Host.Contains('.') && !address.Host.StartsWith('.') && !address.Host.EndsWith('.');
    }
}
