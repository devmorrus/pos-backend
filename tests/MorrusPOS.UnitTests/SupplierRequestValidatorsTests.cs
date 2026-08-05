using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Application.Features.Suppliers.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class SupplierRequestValidatorsTests
{
    private readonly CreateSupplierRequestValidator _createSupplierValidator = new();
    private readonly UpdateSupplierRequestValidator _updateSupplierValidator = new();
    private readonly CreatePurchaseOrderRequestValidator _createPoValidator = new();
    private readonly UpdatePoStatusRequestValidator _updatePoStatusValidator = new();
    private readonly CreateSupplierPaymentRequestValidator _paymentValidator = new();

    // ---- Supplier Create ----

    [Fact]
    public void CreateSupplier_ValidInput_Should_Pass()
    {
        var request = new CreateSupplierRequest(
            Name: "PT Supplier Jaya",
            ContactPerson: "Budi",
            Phone: "+628123456789",
            Email: "budi@supplier.com",
            Address: "Jl. Raya No. 1"
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateSupplier_EmptyName_Should_Fail()
    {
        var request = new CreateSupplierRequest(
            Name: "",
            ContactPerson: null,
            Phone: null,
            Email: null,
            Address: null
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateSupplier_NameTooLong_Should_Fail()
    {
        var request = new CreateSupplierRequest(
            Name: new string('A', 101),
            ContactPerson: null,
            Phone: null,
            Email: null,
            Address: null
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void CreateSupplier_InvalidPhone_Should_Fail()
    {
        var request = new CreateSupplierRequest(
            Name: "PT Test",
            ContactPerson: null,
            Phone: "abc-invalid",
            Email: null,
            Address: null
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Phone);
    }

    [Fact]
    public void CreateSupplier_InvalidEmail_Should_Fail()
    {
        var request = new CreateSupplierRequest(
            Name: "PT Test",
            ContactPerson: null,
            Phone: null,
            Email: "not-an-email",
            Address: null
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ---- Supplier Update ----

    [Fact]
    public void UpdateSupplier_ValidInput_Should_Pass()
    {
        var request = new UpdateSupplierRequest(
            Name: "PT Supplier Baru",
            ContactPerson: "Ani",
            Phone: "081234567890",
            Email: "ani@supplier.com",
            Address: "Jl. Baru No. 2",
            IsActive: true
        );

        var result = _updateSupplierValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ---- Purchase Order Create ----

    [Fact]
    public void CreatePO_ValidCash_Should_Pass()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreatePO_ValidTempo_Should_Pass()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "tempo",
            DueDate: DateTime.UtcNow.AddDays(30),
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 5, 10000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreatePO_EmptySupplier_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.Empty,
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.SupplierId);
    }

    [Fact]
    public void CreatePO_InvalidPaymentType_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "kredit",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PaymentType);
    }

    [Fact]
    public void CreatePO_TempoWithoutDueDate_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "tempo",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void CreatePO_TempoWithPastDueDate_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "tempo",
            DueDate: DateTime.UtcNow.AddDays(-5),
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DueDate);
    }

    [Fact]
    public void CreatePO_EmptyItems_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>()
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    [Fact]
    public void CreatePO_ItemQtyZero_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 0, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].Qty");
    }

    [Fact]
    public void CreatePO_ItemUnitCostZero_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 0)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].UnitCost");
    }

    [Fact]
    public void CreatePO_DuplicateProducts_Should_Fail()
    {
        var sameProductId = Guid.NewGuid();
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(sameProductId, 10, 5000),
                new(sameProductId, 5, 3000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }

    // ---- Update PO Status ----

    [Fact]
    public void UpdatePoStatus_InvalidStatus_Should_Fail()
    {
        var request = new UpdatePoStatusRequest(Status: "approved");

        var result = _updatePoStatusValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void UpdatePoStatus_Completed_Should_Pass()
    {
        var request = new UpdatePoStatusRequest(Status: "completed");

        var result = _updatePoStatusValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdatePoStatus_Pending_Should_Pass()
    {
        var request = new UpdatePoStatusRequest(Status: "pending");

        var result = _updatePoStatusValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdatePoStatus_Received_Should_Fail()
    {
        var request = new UpdatePoStatusRequest(Status: "received");

        var result = _updatePoStatusValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    // ---- Supplier Payment ----

    [Fact]
    public void Payment_ValidInput_Should_Pass()
    {
        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: Guid.NewGuid(),
            Amount: 500000,
            PaymentMethod: "Transfer Bank",
            ReferenceNumber: "TRF-2026-001"
        );

        var result = _paymentValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Payment_ZeroAmount_Should_Fail()
    {
        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: Guid.NewGuid(),
            Amount: 0,
            PaymentMethod: "Transfer Bank",
            ReferenceNumber: null
        );

        var result = _paymentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Payment_EmptyMethod_Should_Fail()
    {
        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: Guid.NewGuid(),
            Amount: 100000,
            PaymentMethod: "",
            ReferenceNumber: null
        );

        var result = _paymentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void Payment_MethodTooLong_Should_Fail()
    {
        var request = new CreateSupplierPaymentRequest(
            PurchaseOrderId: Guid.NewGuid(),
            Amount: 100000,
            PaymentMethod: new string('A', 31),
            ReferenceNumber: null
        );

        var result = _paymentValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.PaymentMethod);
    }

    [Fact]
    public void CreateSupplier_ValidSpecialChars_Should_Pass()
    {
        var request = new CreateSupplierRequest(
            Name: "Supplier A (Jakarta) / L'Oreal & Co.",
            ContactPerson: "Budi",
            Phone: "+62 812-3456-7890",
            Email: "budi@supplier.com",
            Address: "Jl. Raya No. 1"
        );

        var result = _createSupplierValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreatePO_QtyTooLarge_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 1000000000m, 5000)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].Qty");
    }

    [Fact]
    public void CreatePO_UnitCostTooLarge_Should_Fail()
    {
        var request = new CreatePurchaseOrderRequest(
            SupplierId: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            PaymentType: "cash",
            DueDate: null,
            Items: new List<PurchaseOrderItemRequest>
            {
                new(Guid.NewGuid(), 10, 1000000000000m)
            }
        );

        var result = _createPoValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Items[0].UnitCost");
    }
}
