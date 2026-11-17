using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mango.Services.ProductAPI.Models.Dto;
using Mango.Services.ProductAPI.Tests.TestSupport;
using Xunit;

namespace Mango.Services.ProductAPI.Tests.Integration;

[Trait("Category", "Integration")]
public class PriceApiAccessTests
{
    [Theory]
    [InlineData("/api/product", true)]
    [InlineData("/api/product/221", false)]
    public async Task CatalogReadsStayPublic(string path, bool list)
    {
        await using var fixture = await PriceApiFixture.StartAsync();
        Assert.Null(fixture.Client.DefaultRequestHeaders.Authorization);

        using var response = await fixture.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.NotNull(envelope);
        Assert.True(envelope.IsSuccess);
        var data = Assert.IsType<JsonElement>(envelope.Result);
        if (list) Assert.Equal(2, data.GetArrayLength());
        else Assert.Equal(PriceBoundaryData.TargetId, data.GetProperty("productId").GetInt32());
        Assert.Equal(0, fixture.SaveAttempts);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task AnonymousWritesStillRequireAuthentication(string method)
    {
        await using var fixture = await PriceApiFixture.StartAsync();
        var before = await fixture.SnapshotAsync();

        using var response = await fixture.WriteAsync(method, PriceBoundaryData.Request(id: PriceBoundaryData.TargetId), authenticated: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        Assert.Equal(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.SaveAttempts);
    }
}
