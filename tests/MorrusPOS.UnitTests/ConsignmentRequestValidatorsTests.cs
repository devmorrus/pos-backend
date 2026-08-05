using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Application.Features.Consignments.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ConsignmentRequestValidatorsTests
{
    private readonly CreateConsignmentRequestValidator _createConsignmentValidator = new();
    private readonly UpdateConsignmentStatusRequestValidator _updateConsignmentStatusValidator = new();
    private readonly CreateConsignmentSettlementRequestValidator _createSettlementValidator = new();
    private readonly UpdateConsignmentSettlementStatusRequestValidator _updateSettlementStatusValidator = new();

    [Fact]
    public void CreateConsignment_ValidInput_Should_Pass()
    {
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000, 7500)
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateConsignment_EmptySupplier_Should_Fail()
    {
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.Empty,
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000, 7500)
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SupplierId);
    }

    [Fact]
    public void CreateConsignment_DuplicateProducts_Should_Fail()
    {
        var productId = Guid.NewGuid();
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(productId, 10, 5000, 7500),
                new(productId, 5, 5000, 7500)
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void CreateConsignment_UnitPriceLessThanUnitCost_Should_Fail()
    {
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000, 4500) // UnitPrice is less than UnitCost
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].UnitPrice");
    }

    [Fact]
    public void CreateConsignment_QtyTooLarge_Should_Fail()
    {
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(Guid.NewGuid(), 1000000000m, 5000, 7500)
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].Qty");
    }

    [Fact]
    public void CreateConsignment_UnitCostTooLarge_Should_Fail()
    {
        var request = new CreateConsignmentRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            Items: new List<ConsignmentItemRequest>
            {
                new(Guid.NewGuid(), 10, 1000000000000m, 1200000000000m)
            }
        );

        var result = _createConsignmentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].UnitCost");
    }

    [Fact]
    public void UpdateConsignmentStatus_Received_Should_Pass()
    {
        var request = new UpdateConsignmentStatusRequest(Status: "received");

        var result = _updateConsignmentStatusValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateConsignmentStatus_Cancelled_Should_Pass()
    {
        var request = new UpdateConsignmentStatusRequest(Status: "  cancelled ");

        var result = _updateConsignmentStatusValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateConsignmentStatus_InvalidStatus_Should_Fail()
    {
        var request = new UpdateConsignmentStatusRequest(Status: "draft");

        var result = _updateConsignmentStatusValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void CreateSettlement_ValidInput_Should_Pass()
    {
        var request = new CreateConsignmentSettlementRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid()
        );

        var result = _createSettlementValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateSettlementStatus_Settled_Should_Pass()
    {
        var request = new UpdateConsignmentSettlementStatusRequest(Status: "settled");

        var result = _updateSettlementStatusValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateSettlementStatus_Invalid_Should_Fail()
    {
        var request = new UpdateConsignmentSettlementStatusRequest(Status: "pending");

        var result = _updateSettlementStatusValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }
}
