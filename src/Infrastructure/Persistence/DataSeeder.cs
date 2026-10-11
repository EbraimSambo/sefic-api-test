using Microsoft.EntityFrameworkCore;
using Sefic.Domain.Partners;

namespace Sefic.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(SeficDbContext db, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Partners.AnyAsync(ct)) return;
        var now = clock.GetUtcNow().UtcDateTime;
        db.Partners.AddRange(
            Partner.Create("Ana Silva", "001234567", PartnerType.Customer, "ana.silva@example.com", 5000m, now),
            Partner.Create("Bruno Costa Lda", "502345678", PartnerType.Supplier, "geral@brunocosta.example.com", 0m, now),
            Partner.Create("Carla Mendes", "213456789", PartnerType.Both, null, 12500.50m, now),
            Partner.Create("Distribuições Dias SA", "504567890", PartnerType.Both, "compras@dias.example.com", 100000m, now),
            Partner.Create("Eduardo Ferreira", "125678901", PartnerType.Customer, "edu@example.com", 750m, now));
        await db.SaveChangesAsync(ct);
    }
}
