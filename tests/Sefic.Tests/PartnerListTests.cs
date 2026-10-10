using System.Net;

namespace Sefic.Tests;

public class PartnerListTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Type_filters_follow_both_semantics()
    {
        var customer = await api.CreateAsync("TF Cliente", type: "CUSTOMER");
        await api.CreateAsync("TF Fornecedor", type: "SUPPLIER");
        await api.CreateAsync("TF Ambos", type: "BOTH");

        Assert.Equal(["TF Ambos", "TF Cliente"], ApiFixture.Names(await api.ListAsync("?search=TF&type=CUSTOMER")));
        Assert.Equal(["TF Ambos", "TF Fornecedor"], ApiFixture.Names(await api.ListAsync("?search=TF&type=SUPPLIER")));
        Assert.Equal(["TF Ambos"], ApiFixture.Names(await api.ListAsync("?search=TF&type=BOTH")));
        Assert.DoesNotContain("TF Cliente", ApiFixture.Names(await api.ListAsync("?search=TF&type=SUPPLIER")));
        _ = customer;
    }

    [Fact]
    public async Task Inactive_are_hidden_by_default_and_reactivation_restores_them()
    {
        var c = await api.CreateAsync("IA Parceiro");
        var off = await (await api.PutAsync(c, isActive: false)).Content.ReadAsStringAsync();
        Assert.Empty(ApiFixture.Names(await api.ListAsync("?search=IA")).Where(n => n == "IA Parceiro"));
        Assert.Contains("IA Parceiro", ApiFixture.Names(await api.ListAsync("?search=IA&includeInactive=true")));

        var current = System.Text.Json.JsonDocument.Parse(off).RootElement;
        await api.PutAsync(current, isActive: true);
        Assert.Contains("IA Parceiro", ApiFixture.Names(await api.ListAsync("?search=IA")));
    }

    [Fact]
    public async Task Search_is_case_insensitive_on_name_and_matches_normalized_tax()
    {
        await api.CreateAsync("SR Mariana", "987.654.321");
        await api.CreateAsync("SR Outro", "111222333");

        Assert.Equal(["SR Mariana"], ApiFixture.Names(await api.ListAsync("?search=mARIANA")));
        Assert.Equal(["SR Mariana"], ApiFixture.Names(await api.ListAsync("?search=987-654")));
        Assert.Equal(["SR Mariana"], ApiFixture.Names(await api.ListAsync("?search=987%20654.321")));
    }

    [Fact]
    public async Task Search_with_only_separators_does_not_match_every_tax_number()
    {
        await api.CreateAsync("SEP Um");
        await api.CreateAsync("SEP Dois");
        var list = await api.ListAsync("?search=%20-.%20");
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Search_wildcards_are_treated_literally()
    {
        await api.CreateAsync("WC Normal");
        Assert.Equal(0, (await api.ListAsync("?search=%25")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await api.ListAsync("?search=_")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Pagination_counts_before_paging_orders_by_name_then_id_and_handles_out_of_range()
    {
        for (var i = 0; i < 5; i++) await api.CreateAsync("PG Igual");
        await api.CreateAsync("PG Antes");

        var p1 = await api.ListAsync("?search=PG&page=1&pageSize=4");
        Assert.Equal(6, p1.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, p1.GetProperty("page").GetInt32());
        Assert.Equal(4, p1.GetProperty("pageSize").GetInt32());
        var first = p1.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("PG Antes", first[0].GetProperty("name").GetString());

        var p2 = await api.ListAsync("?search=PG&page=2&pageSize=4");
        var all = first.Concat(p2.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(6, all.Length);
        var equalIds = all.Skip(1).Select(i => i.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(equalIds.OrderBy(x => new System.Data.SqlTypes.SqlGuid(x)).ToArray(), equalIds); // SQL Server ordena uniqueidentifier de forma própria

        var beyond = await api.ListAsync("?search=PG&page=99&pageSize=4");
        Assert.Empty(beyond.GetProperty("items").EnumerateArray());
        Assert.Equal(6, beyond.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=abc")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?type=INVALID")]
    [InlineData("?includeInactive=talvez")]
    public async Task Invalid_filters_return_400(string query)
    {
        var response = await api.CreateClient().GetAsync("/api/partners" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
