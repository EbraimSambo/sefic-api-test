using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sefic.Infrastructure.Persistence;

/// <summary>Usada apenas pelo dotnet-ef para criar migrações sem arrancar a API.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<SeficDbContext>
{
    public SeficDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("SEFIC_CONNECTION")
                 ?? "Server=localhost,14333;Database=SeficPartners;User Id=sa;Password=Your_password123;TrustServerCertificate=True";
        return new SeficDbContext(new DbContextOptionsBuilder<SeficDbContext>().UseSqlServer(cs).Options);
    }
}
