using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sefic.Api.Infrastructure;
using Sefic.Application.Partners;
using Sefic.Infrastructure;
using Sefic.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o =>
        // Erros de binding/JSON malformado seguem o mesmo formato dos restantes erros.
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errors = ctx.ModelState.Where(e => e.Value!.Errors.Count > 0).ToDictionary(
                e => string.IsNullOrEmpty(e.Key) || e.Key == "$" ? "body" : e.Key.TrimStart('$', '.'),
                e => e.Value!.Errors.Select(_ => "Valor inválido ou malformado.").ToArray());
            return new ObjectResult(ApiErrors.Create(400, "Pedido inválido",
                "O pedido contém valores malformados.", errors))
            { StatusCode = 400, ContentTypes = { "application/problem+json" } };
        });
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default em falta."));
builder.Services.AddScoped<PartnerService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Configuration.GetValue<bool>("Seed:Enabled"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SeficDbContext>();
    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
}

app.MapControllers();
app.Run();

public partial class Program;
