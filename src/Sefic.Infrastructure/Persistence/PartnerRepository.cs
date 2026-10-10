using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Sefic.Application.Common;
using Sefic.Application.Partners;
using Sefic.Domain.Partners;

namespace Sefic.Infrastructure.Persistence;

public class PartnerRepository(SeficDbContext db) : IPartnerRepository
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task AddAsync(Partner partner, CancellationToken ct)
    {
        db.Partners.Add(partner);
        await SaveChangesAsync(ct);
    }

    public Task<Partner?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        db.Partners.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Partner?> GetAsync(Guid id, CancellationToken ct) =>
        db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task SaveAsync(Partner partner, byte[] expectedVersion, CancellationToken ct)
    {
        // O UPDATE inclui "WHERE RowVersion = @expected": a comparação é feita pelo SQL Server.
        db.Entry(partner).Property(p => p.RowVersion).OriginalValue = expectedVersion;
        await SaveChangesAsync(ct);
    }

    public async Task<PagedResult<Partner>> SearchAsync(PartnerFilter f, CancellationToken ct)
    {
        IQueryable<Partner> query = db.Partners.AsNoTracking();

        if (!f.IncludeInactive) query = query.Where(p => p.IsActive);

        if (f.Type is { } type)
        {
            query = type switch
            {
                PartnerType.Customer => query.Where(p => p.Type == PartnerType.Customer || p.Type == PartnerType.Both),
                PartnerType.Supplier => query.Where(p => p.Type == PartnerType.Supplier || p.Type == PartnerType.Both),
                _ => query.Where(p => p.Type == PartnerType.Both)
            };
        }

        if (f.Name is { } name)
        {
            var tax = f.TaxNumber;
            query = tax is null
                ? query.Where(p => p.Name.Contains(name))
                : query.Where(p => p.Name.Contains(name) || p.TaxNumber.Contains(tax));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);
        return new PagedResult<Partner>(items, total, f.Page, f.PageSize);
    }

    private async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("A entidade foi alterada ou removida por outro pedido. Leia-a novamente e repita a atualização com a Version atual.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            throw new ConflictException("Já existe uma entidade registada com este TaxNumber.");
        }
    }
}
