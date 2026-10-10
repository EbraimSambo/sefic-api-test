using Microsoft.EntityFrameworkCore;
using Sefic.Domain.Partners;

namespace Sefic.Infrastructure.Persistence;

public class SeficDbContext(DbContextOptions<SeficDbContext> options) : DbContext(options)
{
    public DbSet<Partner> Partners => Set<Partner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeficDbContext).Assembly);
    }
}
