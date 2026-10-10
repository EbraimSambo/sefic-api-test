using Sefic.Domain.Partners;

namespace Sefic.Tests;

public class TaxNumberRulesTests
{
    [Theory]
    [InlineData("001.234.567", "001234567")]
    [InlineData(" 001 234-567 ", "001234567")]
    public void Normalizes_separators_preserving_leading_zeros(string input, string expected)
    {
        Assert.True(TaxNumberRules.TryNormalize(input, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    [InlineData("12345678A")]
    [InlineData("١٢٣٤٥٦٧٨٩")]
    public void Rejects_invalid_values(string? input) =>
        Assert.False(TaxNumberRules.TryNormalize(input, out _));

    [Fact]
    public void Separator_only_search_normalizes_to_empty() =>
        Assert.Equal(string.Empty, TaxNumberRules.StripSeparators(" - . "));
}
