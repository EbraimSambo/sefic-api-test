namespace Sefic.Domain.Partners;

public static class TaxNumberRules
{
    public const int Length = 9;

    /// <summary>Remove espaços, pontos e hífenes.</summary>
    public static string StripSeparators(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-'));
    }

    /// <summary>Normaliza e exige exatamente nove dígitos ASCII.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = StripSeparators(value);
        return normalized.Length == Length && normalized.All(c => c is >= '0' and <= '9');
    }
}
