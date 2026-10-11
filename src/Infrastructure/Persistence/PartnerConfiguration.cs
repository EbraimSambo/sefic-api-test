using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sefic.Domain.Partners;

namespace Sefic.Infrastructure.Persistence;

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    public void Configure(EntityTypeBuilder<Partner> b)
    {
        b.ToTable("Partners");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();

        // Colação explícita para a procura por nome ser insensível a maiúsculas.
        b.Property(p => p.Name).HasMaxLength(Partner.NameMaxLength).IsRequired()
            .UseCollation("Latin1_General_100_CI_AS");
        b.Property(p => p.TaxNumber).HasMaxLength(TaxNumberRules.Length).IsUnicode(false).IsFixedLength().IsRequired();
        b.Property(p => p.Type).HasConversion<string>().HasMaxLength(10).IsUnicode(false).IsRequired();
        b.Property(p => p.Email).HasMaxLength(Partner.EmailMaxLength);
        b.Property(p => p.CreditLimit).HasColumnType("decimal(18,2)");
        // Datas guardadas e lidas sempre como UTC (Kind=Utc, serializadas com "Z").
        b.Property(p => p.CreatedAt).HasColumnType("datetime2(3)").HasConversion(UtcConverter);
        b.Property(p => p.UpdatedAt).HasColumnType("datetime2(3)").HasConversion(UtcConverter);
        b.Property(p => p.RowVersion).IsRowVersion();

        // Unicidade garantida pela base de dados (inclui entidades inativas).
        b.HasIndex(p => p.TaxNumber).IsUnique();
        b.HasIndex(p => new { p.Name, p.Id });
        b.ToTable(t => t.HasCheckConstraint("CK_Partners_CreditLimit", "[CreditLimit] >= 0 AND [CreditLimit] <= 1000000"));
    }
}
