using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sefic.Tests;

public class PartnerGetUpdateTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Get_unknown_id_returns_404_and_malformed_id_400()
    {
        var client = api.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/partners/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/partners/abc")).StatusCode);
    }

    [Fact]
    public async Task Deactivated_partner_is_still_returned_by_id()
    {
        var c = await api.CreateAsync();
        await api.PutAsync(c, isActive: false);
        var got = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/partners/{c.GetProperty("id").GetGuid()}");
        Assert.False(got.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Put_updates_data_changes_version_and_keeps_created_at()
    {
        var c = await api.CreateAsync("Original", creditLimit: 5m);
        var response = await api.PutAsync(c, name: " Alterado ", creditLimit: 7.5m);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var u = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alterado", u.GetProperty("name").GetString());
        Assert.Equal(7.5m, u.GetProperty("creditLimit").GetDecimal());
        Assert.NotEqual(c.GetProperty("version").GetString(), u.GetProperty("version").GetString());
        Assert.Equal(c.GetProperty("createdAt").GetDateTime(), u.GetProperty("createdAt").GetDateTime());
        Assert.True(u.GetProperty("updatedAt").GetDateTime() >= c.GetProperty("updatedAt").GetDateTime());
    }

    [Fact]
    public async Task Put_keeping_own_tax_is_allowed_but_taking_another_returns_409_and_keeps_data()
    {
        var a = await api.CreateAsync("Parceiro A", "111.111.111");
        var b = await api.CreateAsync("Parceiro B", "222222222");

        Assert.Equal(HttpStatusCode.OK, (await api.PutAsync(a, name: "Parceiro A2", tax: "111 111 111")).StatusCode);

        var conflict = await api.PutAsync(b, name: "Parceiro B2", tax: "111111111");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var stored = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/partners/{b.GetProperty("id").GetGuid()}");
        Assert.Equal("Parceiro B", stored.GetProperty("name").GetString());
        Assert.Equal("222222222", stored.GetProperty("taxNumber").GetString());
        Assert.Equal(b.GetProperty("version").GetString(), stored.GetProperty("version").GetString());
    }

    [Fact]
    public async Task Put_with_stale_version_returns_409_without_overwriting()
    {
        var c = await api.CreateAsync("Nome V1");
        Assert.Equal(HttpStatusCode.OK, (await api.PutAsync(c, name: "Nome V2")).StatusCode);

        var stale = await api.PutAsync(c, name: "Nome V3");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var stored = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/partners/{c.GetProperty("id").GetGuid()}");
        Assert.Equal("Nome V2", stored.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Concurrent_puts_with_same_version_yield_one_200_and_one_409()
    {
        var c = await api.CreateAsync("Base");
        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(0, 6).Select(async i =>
        {
            await gate.Task;
            return (await api.PutAsync(c, name: $"Writer {i}")).StatusCode;
        }).ToArray();
        gate.SetResult();
        var codes = await Task.WhenAll(tasks);

        Assert.Single(codes, HttpStatusCode.OK);
        Assert.Equal(5, codes.Count(x => x == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Put_unknown_id_returns_404()
    {
        var c = await api.CreateAsync();
        var response = await api.CreateClient().PutAsJsonAsync($"/api/partners/{Guid.NewGuid()}", new
        {
            name = "X1", taxNumber = api.NewTax(), type = "BOTH", creditLimit = 1, isActive = true,
            version = c.GetProperty("version").GetString()
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("not-base64!")]
    [InlineData("AAAA")]
    [InlineData("")]
    public async Task Put_with_malformed_or_missing_version_returns_400_and_changes_nothing(string version)
    {
        var c = await api.CreateAsync("Intacto");
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync(c, name: "Mudado", version: version)).StatusCode);
        var stored = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/partners/{c.GetProperty("id").GetGuid()}");
        Assert.Equal("Intacto", stored.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Put_missing_required_fields_returns_400()
    {
        var c = await api.CreateAsync();
        var response = await api.CreateClient().PutAsJsonAsync($"/api/partners/{c.GetProperty("id").GetGuid()}",
            new { name = "Ok name", version = c.GetProperty("version").GetString() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_invalid_credit_limit_returns_400_and_changes_nothing()
    {
        var c = await api.CreateAsync("Intacto2");
        Assert.Equal(HttpStatusCode.BadRequest, (await api.PutAsync(c, name: "Mudado", creditLimit: 10.999m)).StatusCode);
        var stored = await api.CreateClient().GetFromJsonAsync<JsonElement>($"/api/partners/{c.GetProperty("id").GetGuid()}");
        Assert.Equal("Intacto2", stored.GetProperty("name").GetString());
    }
}
