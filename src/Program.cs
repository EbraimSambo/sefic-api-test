using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sefic.Api.Errors;
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
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Sefic API — clientes e fornecedores";
    doc.Info.Version = "v1";
    doc.Info.Description = "Gestão de clientes e fornecedores com NIF único e concorrência otimista (campo `version`). " +
        "Erros em `application/problem+json`. Para atualizar, leia primeiro a entidade e envie a `version` recebida.";
    return Task.CompletedTask;
}));
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

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Sefic API v1");
        o.RoutePrefix = "swagger";
        o.DocumentTitle = "Sefic API";
    });
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.Run();

public partial class Program;
