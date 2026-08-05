using System;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Products.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ProductRequestValidatorsTests
{
    private readonly CreateProductRequestValidator _createProductValidator = new();
    private readonly UpdateProductRequestValidator _updateProductValidator = new();
    private readonly CreateCategoryRequestValidator _createCategoryValidator = new();
    private readonly UpdateCategoryRequestValidator _updateCategoryValidator = new();

    [Fact]
    public void CreateProductRequest_ValidInput_Should_Pass()
    {
        var request = new CreateProductRequest(
            CategoryId: Guid.NewGuid(),
            Sku: "PROD-001_A",
            Name: "Kopi Hitam Premium",
            Barcode: "123456789012",
            BasePrice: 15000,
            CostPrice: 10000,
            Unit: "pcs",
            IsConsignment: false
        );

        var result = _createProductValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("PROD 001")] // contains spaces
    [InlineData("PR")] // too short
    [InlineData("PROD@001")] // invalid character
    public void CreateProductRequest_InvalidSku_Should_Fail(string sku)
    {
        var request = new CreateProductRequest(
            CategoryId: Guid.NewGuid(),
            Sku: sku,
            Name: "Kopi Hitam Premium",
            Barcode: "123456789012",
            BasePrice: 15000,
            CostPrice: 10000,
            Unit: "pcs",
            IsConsignment: false
        );

        var result = _createProductValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Sku);
    }

    [Theory]
    [InlineData("1234567")] // too short (7 digits)
    [InlineData("1234567890123456789")] // too long (19 digits)
    [InlineData("12345EAN12")] // non-numeric
    public void CreateProductRequest_InvalidBarcode_Should_Fail(string barcode)
    {
        var request = new CreateProductRequest(
            CategoryId: Guid.NewGuid(),
            Sku: "PROD-001",
            Name: "Kopi Hitam Premium",
            Barcode: barcode,
            BasePrice: 15000,
            CostPrice: 10000,
            Unit: "pcs",
            IsConsignment: false
        );

        var result = _createProductValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Barcode);
    }

    [Theory]
    [InlineData(10000, 10000)] // CostPrice == BasePrice (fail: selling price must be higher than cost)
    [InlineData(10000, 12000)] // CostPrice > BasePrice (fail: loss selling)
    [InlineData(0, 0)] // BasePrice == 0 (fail: selling price must be > 0)
    [InlineData(-1000, 500)] // BasePrice < 0 (fail)
    [InlineData(10000, -100)] // CostPrice < 0 (fail)
    public void CreateProductRequest_InvalidPricing_Should_Fail(decimal basePrice, decimal costPrice)
    {
        var request = new CreateProductRequest(
            CategoryId: Guid.NewGuid(),
            Sku: "PROD-001",
            Name: "Kopi Hitam Premium",
            Barcode: "123456789012",
            BasePrice: basePrice,
            CostPrice: costPrice,
            Unit: "pcs",
            IsConsignment: false
        );

        var result = _createProductValidator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [InlineData("C")] // too short
    [InlineData("")] // empty
    public void CreateCategoryRequest_InvalidName_Should_Fail(string name)
    {
        var request = new CreateCategoryRequest(
            Name: name,
            ParentId: null
        );

        var result = _createCategoryValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
