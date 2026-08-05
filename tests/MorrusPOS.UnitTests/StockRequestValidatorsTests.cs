using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Stock.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class StockRequestValidatorsTests
{
    private readonly CreateStockOpnameRequestValidator _opnameValidator = new();
    private readonly CreateStockTransferRequestValidator _transferValidator = new();

    // ---- Stock Opname ----

    [Fact]
    public void StockOpname_ValidInput_Should_Pass()
    {
        var request = new CreateStockOpnameRequest(
            OutletId: Guid.NewGuid(),
            Items: new List<StockOpnameItemRequest>
            {
                new(Guid.NewGuid(), 10),
                new(Guid.NewGuid(), 0)
            }
        );

        var result = _opnameValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StockOpname_EmptyItems_Should_Fail()
    {
        var request = new CreateStockOpnameRequest(
            OutletId: Guid.NewGuid(),
            Items: new List<StockOpnameItemRequest>()
        );

        var result = _opnameValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void StockOpname_NegativePhysicalQty_Should_Fail()
    {
        var request = new CreateStockOpnameRequest(
            OutletId: Guid.NewGuid(),
            Items: new List<StockOpnameItemRequest>
            {
                new(Guid.NewGuid(), -5)
            }
        );

        var result = _opnameValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].PhysicalQty");
    }

    [Fact]
    public void StockOpname_DuplicateProducts_Should_Fail()
    {
        var sameProductId = Guid.NewGuid();
        var request = new CreateStockOpnameRequest(
            OutletId: Guid.NewGuid(),
            Items: new List<StockOpnameItemRequest>
            {
                new(sameProductId, 10),
                new(sameProductId, 20)
            }
        );

        var result = _opnameValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    // ---- Stock Transfer ----

    [Fact]
    public void StockTransfer_ValidInput_Should_Pass()
    {
        var request = new CreateStockTransferRequest(
            FromOutletId: Guid.NewGuid(),
            ToOutletId: Guid.NewGuid(),
            Items: new List<StockTransferItemRequest>
            {
                new(Guid.NewGuid(), 5)
            }
        );

        var result = _transferValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void StockTransfer_SameOutlet_Should_Fail()
    {
        var sameOutletId = Guid.NewGuid();
        var request = new CreateStockTransferRequest(
            FromOutletId: sameOutletId,
            ToOutletId: sameOutletId,
            Items: new List<StockTransferItemRequest>
            {
                new(Guid.NewGuid(), 5)
            }
        );

        var result = _transferValidator.TestValidate(request);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void StockTransfer_ZeroQty_Should_Fail()
    {
        var request = new CreateStockTransferRequest(
            FromOutletId: Guid.NewGuid(),
            ToOutletId: Guid.NewGuid(),
            Items: new List<StockTransferItemRequest>
            {
                new(Guid.NewGuid(), 0)
            }
        );

        var result = _transferValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].Qty");
    }

    [Fact]
    public void StockTransfer_DuplicateProducts_Should_Fail()
    {
        var sameProductId = Guid.NewGuid();
        var request = new CreateStockTransferRequest(
            FromOutletId: Guid.NewGuid(),
            ToOutletId: Guid.NewGuid(),
            Items: new List<StockTransferItemRequest>
            {
                new(sameProductId, 5),
                new(sameProductId, 3)
            }
        );

        var result = _transferValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }
}
