namespace Sefic.Domain.Partners;

public class Partner
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const decimal CreditLimitMax = 1_000_000m;

    private Partner() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string TaxNumber { get; private set; } = string.Empty;
    public PartnerType Type { get; private set; }
    public string? Email { get; private set; }
    public decimal CreditLimit { get; private set; }
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Os valores já devem estar validados e normalizados.</summary>
    public static Partner Create(string name, string taxNumber, PartnerType type, string? email,
        decimal creditLimit, DateTime utcNow) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        TaxNumber = taxNumber,
        Type = type,
        Email = email,
        CreditLimit = creditLimit,
        IsActive = true,
        CreatedAt = utcNow,
        UpdatedAt = utcNow
    };

    public void Update(string name, string taxNumber, PartnerType type, string? email,
        decimal creditLimit, bool isActive, DateTime utcNow)
    {
        Name = name;
        TaxNumber = taxNumber;
        Type = type;
        Email = email;
        CreditLimit = creditLimit;
        IsActive = isActive;
        UpdatedAt = utcNow;
    }
}
