using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mango.Services.ProductAPI.Models.Dto;
using Mango.Services.ProductAPI.Tests.TestSupport;
using Xunit;

namespace Mango.Services.ProductAPI.Tests.Integration;

[Trait("Category", "Integration")]
public class PriceApiBoundaryTests
{
    public static IEnumerable<object[]> RejectedPrices()
    {
        foreach (var method in new[] { "POST", "PUT" })
        foreach (var boundary in PriceBoundaryData.Prices().Where(p => !p.Valid))
            yield return [method, boundary.Price];
    }

    [Theory]
    [MemberData(nameof(RejectedPrices))]
    public async Task OutOfRangePriceIsRejectedBeforeAnySave(string method, double price)
    {
        await using var fixture = await PriceApiFixture.StartAsync();
        var before = await fixture.SnapshotAsync();
        var payload = PriceBoundaryData.Request(price, id: method == "PUT" ? PriceBoundaryData.TargetId : 0);

        using var response = await fixture.WriteAsync(method, payload);

        await AssertValidationProblem(response, "Price");
        Assert.Equal(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.SaveAttempts);
        Assert.Equal(BitConverter.DoubleToInt64Bits(price), BitConverter.DoubleToInt64Bits(payload.Price));
    }

    public static IEnumerable<object[]> RejectedNames()
    {
        foreach (var method in new[] { "POST", "PUT" })
        foreach (var name in new string?[] { null, "", " " })
            yield return [method, name!];
    }

    [Theory]
    [MemberData(nameof(RejectedNames))]
    public async Task RequiredNameIsRejectedBeforeAnySave(string method, string? name)
    {
        await using var fixture = await PriceApiFixture.StartAsync();
        var before = await fixture.SnapshotAsync();
        var payload = PriceBoundaryData.Request(name: name, id: method == "PUT" ? PriceBoundaryData.TargetId : 0);

        using var response = await fixture.WriteAsync(method, payload);

        await AssertValidationProblem(response, "Name");
        Assert.Equal(before, await fixture.SnapshotAsync());
        Assert.Equal(0, fixture.SaveAttempts);
    }

    public static IEnumerable<object[]> AcceptedPrices()
    {
        foreach (var method in new[] { "POST", "PUT" })
        foreach (var boundary in PriceBoundaryData.Prices().Where(p => p.Valid))
            yield return [method, boundary.Price];
    }

    [Theory]
    [MemberData(nameof(AcceptedPrices))]
    public async Task AcceptedPriceIsPersistedWithoutRounding(string method, double price)
    {
        await using var fixture = await PriceApiFixture.StartAsync();
        var before = await fixture.RowsAsync();
        var payload = PriceBoundaryData.Request(price, "Accepted price product", method == "PUT" ? PriceBoundaryData.TargetId : 0);

        using var response = await fixture.WriteAsync(method, payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ResponseDto>();
        Assert.NotNull(envelope);
        Assert.True(envelope.IsSuccess);
        var result = Assert.IsType<JsonElement>(envelope.Result).Deserialize<ProductDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        Assert.Equal(BitConverter.DoubleToInt64Bits(price), BitConverter.DoubleToInt64Bits(result.Price));
        Assert.Equal(payload.Name, result.Name);
        Assert.Equal(payload.Description, result.Description);
        Assert.Equal(payload.CategoryName, result.CategoryName);
        Assert.Equal(payload.ImageUrl, result.ImageUrl);
        if (method == "PUT") Assert.Equal(PriceBoundaryData.TargetId, result.ProductId);
        else Assert.DoesNotContain(before, row => row.ProductId == result.ProductId);

        var after = await fixture.RowsAsync();
        var stored = Assert.Single(after, row => row.ProductId == result.ProductId);
        Assert.Equal(BitConverter.DoubleToInt64Bits(price), BitConverter.DoubleToInt64Bits(stored.Price));
        Assert.Equal(payload.Name, stored.Name);
        Assert.Equal(payload.Description, stored.Description);
        Assert.Equal(payload.CategoryName, stored.CategoryName);
        Assert.Equal(payload.ImageUrl, stored.ImageUrl);
        Assert.Equal(method == "POST" ? before.Length + 1 : before.Length, after.Length);
        Assert.Equal(1, fixture.SaveAttempts);
        foreach (var untouched in before.Where(row => method == "POST" || row.ProductId != PriceBoundaryData.TargetId))
            Assert.Equal(JsonSerializer.Serialize(untouched), JsonSerializer.Serialize(Assert.Single(after, row => row.ProductId == untouched.ProductId)));
    }

    private static async Task AssertValidationProblem(HttpResponseMessage response, string member)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        var errors = problem.RootElement.GetProperty("errors").EnumerateObject().ToArray();
        var error = Assert.Single(errors, error => error.Name.Equals(member, StringComparison.OrdinalIgnoreCase));
        Assert.True(error.Value.GetArrayLength() > 0);
    }
}
