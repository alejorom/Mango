using System.ComponentModel.DataAnnotations;
using Mango.Services.ProductAPI.Tests.TestSupport;
using Xunit;

namespace Mango.Services.ProductAPI.Tests.Unit;

[Trait("Category", "Unit")]
public class PriceRangeAnnotationTests
{
    [Theory]
    [InlineData(false, 0.99)]
    [InlineData(false, 1000.01)]
    [InlineData(true, 0.99)]
    [InlineData(true, 1000.01)]
    public void ObservedFractionalPricesAreRejected(bool entity, double price)
    {
        var model = PriceBoundaryData.Model(entity, price);
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(errors, error => error.MemberNames.Contains("Price"));
        Assert.Equal(price, PriceBoundaryData.PriceOf(model));
    }

    public static IEnumerable<object[]> OtherPriceCases()
    {
        foreach (var entity in new[] { false, true })
        foreach (var boundary in PriceBoundaryData.Prices().Where(p => p.Price is not (0.99 or 1000.01)))
            yield return [entity, boundary.Group, boundary.Price, boundary.Valid];
    }

    [Theory]
    [MemberData(nameof(OtherPriceCases))]
    public void RangeMatrixValidatesOriginalValue(bool entity, string group, double price, bool expectedValid)
    {
        Assert.Contains(group, new[] { "B1", "B2", "B3", "B4" });
        var model = PriceBoundaryData.Model(entity, price);
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
        if (expectedValid) Assert.Empty(errors);
        else Assert.Contains(errors, error => error.MemberNames.Contains("Price"));
        Assert.Equal(price, PriceBoundaryData.PriceOf(model));
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    public void RequiredNameRemainsEnforced(bool entity, string? name)
    {
        var model = PriceBoundaryData.Model(entity, 30.25, name);
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true));

        Assert.Contains(errors, error => error.MemberNames.Contains("Name"));
        Assert.DoesNotContain(errors, error => error.MemberNames.Contains("Price"));
        Assert.Equal(30.25, PriceBoundaryData.PriceOf(model));
    }
}
