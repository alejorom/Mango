using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;

namespace Mango.Services.ProductAPI.Tests.TestSupport;

internal static class PriceBoundaryData
{
    public const int TargetId = 221;
    public const int UnrelatedId = 222;

    public static IEnumerable<(string Group, double Price, bool Valid)> Prices()
    {
        foreach (var price in new[] { -1d, 0d, 0.99d, 1000.01d, 1001d }) yield return ("B1", price, false);
        yield return ("B2", Math.BitDecrement(1d), false);
        yield return ("B2", Math.BitIncrement(1000d), false);
        yield return ("B3", 1d, true);
        yield return ("B3", 1000d, true);
        yield return ("B4", 1.01d, true);
        yield return ("B4", 999.99d, true);
    }

    public static ProductDto Request(double price = 30.25, string? name = "Range regression product", int id = 0) => new()
    {
        ProductId = id,
        Name = name!,
        Price = price,
        Description = "Isolated price validation",
        CategoryName = "Regression",
        ImageUrl = "https://example.invalid/range.png"
    };

    public static Product Entity(double price = 30.25, string? name = "Range regression product", int id = TargetId) => new()
    {
        ProductId = id,
        Name = name!,
        Price = price,
        Description = "Isolated price validation",
        CategoryName = "Regression",
        ImageUrl = "https://example.invalid/range.png"
    };

    public static object Model(bool entity, double price, string? name = "Range regression product") =>
        entity ? Entity(price, name) : Request(price, name);

    public static double PriceOf(object model) => model switch
    {
        Product entity => entity.Price,
        ProductDto dto => dto.Price,
        _ => throw new ArgumentException("Unexpected validation model", nameof(model))
    };
}
