using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sefic.Application.Partners;
using Sefic.Infrastructure.Persistence;

namespace Sefic.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SeficDbContext>(o => o.UseSqlServer(connectionString,
            sql => sql.MigrationsAssembly(typeof(SeficDbContext).Assembly.FullName)));
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        return services;
    }
}
