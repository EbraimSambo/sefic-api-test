using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sefic.Infrastructure.Persistence;

namespace Sefic.Tests;

/// <summary>Cria uma base de dados SQL Server própria (e descartável) por classe de testes.</summary>
public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly string BaseConnection = Environment.GetEnvironmentVariable("SEFIC_TEST_CONNECTION")
        ?? "Server=localhost,14333;User Id=sa;Password=Your_password123;TrustServerCertificate=True";

    private readonly string _database = $"SeficTests_{Guid.NewGuid():N}";
    private int _taxCounter = Random.Shared.Next(100_000_000, 800_000_000);

    public string ConnectionString => new SqlConnectionStringBuilder(BaseConnection) { InitialCatalog = _database }.ConnectionString;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Seed:Enabled", "false");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SeficDbContext>().Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SeficDbContext>().Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }

    /// <summary>NIF válido e único dentro desta base de dados.</summary>
    public string NewTax() => Interlocked.Increment(ref _taxCounter).ToString("D9");

    public Task<HttpResponseMessage> PostAsync(object body) =>
        CreateClient().PostAsJsonAsync("/api/partners", body);

    public async Task<JsonElement> CreateAsync(string name = "Parceiro Teste", string? tax = null,
        string type = "CUSTOMER", string? email = null, decimal creditLimit = 100m)
    {
        var response = await PostAsync(new { name, taxNumber = tax ?? NewTax(), type, email, creditLimit });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public Task<HttpResponseMessage> PutAsync(JsonElement current, string? name = null, string? tax = null,
        string? type = null, bool? isActive = null, string? version = null, decimal? creditLimit = null) =>
        CreateClient().PutAsJsonAsync($"/api/partners/{current.GetProperty("id").GetGuid()}", new
        {
            name = name ?? current.GetProperty("name").GetString(),
            taxNumber = tax ?? current.GetProperty("taxNumber").GetString(),
            type = type ?? current.GetProperty("type").GetString(),
            email = current.GetProperty("email").GetString(),
            creditLimit = creditLimit ?? current.GetProperty("creditLimit").GetDecimal(),
            isActive = isActive ?? current.GetProperty("isActive").GetBoolean(),
            version = version ?? current.GetProperty("version").GetString()
        });

    public async Task<JsonElement> ListAsync(string query)
    {
        var response = await CreateClient().GetAsync("/api/partners" + query);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string[] Names(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToArray();

    public async Task<int> CountRowsAsync()
    {
        using var scope = Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SeficDbContext>().Partners.CountAsync();
    }
}
