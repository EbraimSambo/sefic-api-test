using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sefic.Tests;

public class PartnerCreateTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Create_normalizes_name_and_tax_and_returns_location()
    {
        var response = await api.PostAsync(new { name = " Ana Silva ", taxNumber = "001.234.567", type = "CUSTOMER", email = "  ", creditLimit = 10 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        Assert.EndsWith($"/api/partners/{id}", response.Headers.Location!.ToString());
        Assert.Equal("Ana Silva", body.GetProperty("name").GetString());
        Assert.Equal("001234567", body.GetProperty("taxNumber").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("email").ValueKind);
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("version").GetString()));
        Assert.Equal(body.GetProperty("createdAt").GetDateTime(), body.GetProperty("updatedAt").GetDateTime());
    }

    [Fact]
    public async Task Same_tax_in_other_representation_returns_409_even_after_deactivation()
    {
        var created = await api.CreateAsync("Ana Silva", "001.234.568");
        Assert.Equal(HttpStatusCode.Conflict,
            (await api.PostAsync(new { name = "Outra", taxNumber = "001 234 568", type = "SUPPLIER", creditLimit = 1 })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await api.PutAsync(created, isActive: false)).StatusCode);
        var again = await api.PostAsync(new { name = "Outra", taxNumber = "001-234-568", type = "SUPPLIER", creditLimit = 1 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var problem = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task Concurrent_creates_with_same_tax_yield_one_201_and_one_409()
    {
        var tax = api.NewTax();
        var before = await api.CountRowsAsync();
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 8).Select(async i =>
        {
            var client = api.CreateClient();
            await gate.Task;
            return (await client.PostAsJsonAsync("/api/partners",
                new { name = $"Concorrente {i}", taxNumber = tax, type = "BOTH", creditLimit = 1 })).StatusCode;
        }).ToArray();
        gate.SetResult();
        var codes = await Task.WhenAll(tasks);

        Assert.Single(codes, HttpStatusCode.Created);
        Assert.Equal(7, codes.Count(c => c == HttpStatusCode.Conflict));
        Assert.Equal(before + 1, await api.CountRowsAsync());
    }

    public static TheoryData<object> InvalidBodies() => new()
    {
        new { name = "Ana", taxNumber = "123456789", type = "CUSTOMER", creditLimit = 10.999 },
        new { name = "Ana", taxNumber = "12345678A", type = "CUSTOMER", creditLimit = 1 },
        new { name = "Ana", taxNumber = "12345678", type = "CUSTOMER", creditLimit = 1 },
        new { name = "Ana", taxNumber = "١٢٣٤٥٦٧٨٩", type = "CUSTOMER", creditLimit = 1 },
        new { name = " A ", taxNumber = "123456789", type = "CUSTOMER", creditLimit = 1 },
        new { name = new string('x', 101), taxNumber = "123456789", type = "CUSTOMER", creditLimit = 1 },
        new { name = "Ana", taxNumber = "123456789", type = "OTHER", creditLimit = 1 },
        new { name = "Ana", taxNumber = "123456789", type = "CUSTOMER", creditLimit = -1 },
        new { name = "Ana", taxNumber = "123456789", type = "CUSTOMER", creditLimit = 1000000.01 },
        new { name = "Ana", taxNumber = "123456789", type = "CUSTOMER", email = "nao-e-email", creditLimit = 1 },
    };

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Invalid_requests_return_400_and_persist_nothing(object body)
    {
        var before = await api.CountRowsAsync();
        var response = await api.PostAsync(body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(before, await api.CountRowsAsync());
    }

    [Fact]
    public async Task Malformed_json_returns_400()
    {
        var response = await api.CreateClient().PostAsync("/api/partners",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_limit_is_persisted_with_two_decimals_and_email_is_trimmed()
    {
        var c = await api.CreateAsync(email: "  a@b.pt ", creditLimit: 1000000m);
        Assert.Equal("a@b.pt", c.GetProperty("email").GetString());
        Assert.Equal(1000000m, c.GetProperty("creditLimit").GetDecimal());
    }
}
