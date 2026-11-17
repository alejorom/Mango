using System.ComponentModel.DataAnnotations;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Mango.Services.ProductAPI.Tests.TestSupport;
using Xunit;

namespace Mango.Services.ProductAPI.Tests.Unit;

[Trait("Category", "Unit")]
public class ProductContractTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(42.5)]
    public void ValidProductAcceptsInclusivePriceLimits(double price)
    {
        var dto = ProductTestData.Dto(price: price);
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ProductNameIsRequired(string? name)
    {
        var dto = ProductTestData.Dto(name: name);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ProductDto.Name)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(0.99)]
    [InlineData(1000.01)]
    public void ProductPriceOutsideRangeIsRejected(double price)
    {
        var dto = ProductTestData.Dto(price: price);
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(ProductDto.Price)));
    }

    [Fact]
    public void ProductionMappingIsValidAndPreservesAllFieldsInBothDirections()
    {
        var configuration = MappingConfig.RegisterMaps();
        configuration.AssertConfigurationIsValid();
        var mapper = configuration.CreateMapper();
        var source = ProductTestData.Dto();

        var entity = mapper.Map<Product>(source);

        Assert.Equal(source.ProductId, entity.ProductId);
        Assert.Equal(source.Name, entity.Name);
        Assert.Equal(source.Price, entity.Price);
        Assert.Equal(source.Description, entity.Description);
        Assert.Equal(source.CategoryName, entity.CategoryName);
        Assert.Equal(source.ImageUrl, entity.ImageUrl);
        var roundTrip = mapper.Map<ProductDto>(entity);
        Assert.Equivalent(source, roundTrip, strict: true);
    }

    [Fact]
    public void ProductionMappingUpdatesExistingEntityWithoutLosingFields()
    {
        var mapper = MappingConfig.RegisterMaps().CreateMapper();
        var existing = ProductTestData.Entity();
        var changed = new ProductDto
        {
            ProductId = existing.ProductId, Name = "Updated name", Price = 1000,
            Description = "Updated description", CategoryName = "Updated category", ImageUrl = "https://example.invalid/updated.png"
        };

        var result = mapper.Map(changed, existing);

        Assert.Same(existing, result);
        Assert.Equal(changed.ProductId, existing.ProductId);
        Assert.Equal(changed.Name, existing.Name);
        Assert.Equal(changed.Price, existing.Price);
        Assert.Equal(changed.Description, existing.Description);
        Assert.Equal(changed.CategoryName, existing.CategoryName);
        Assert.Equal(changed.ImageUrl, existing.ImageUrl);
    }
}
