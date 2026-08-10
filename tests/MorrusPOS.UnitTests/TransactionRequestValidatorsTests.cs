using System;
using System.Collections.Generic;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Transactions.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class TransactionRequestValidatorsTests
{
    private readonly OpenSessionRequestValidator _openSessionValidator = new();
    private readonly CloseSessionRequestValidator _closeSessionValidator = new();
    private readonly CheckoutRequestValidator _checkoutValidator = new();
    private readonly VoidTransactionRequestValidator _voidValidator = new();
    private readonly RefundTransactionRequestValidator _refundValidator = new();

    [Theory]
    [InlineData(100000, true)]
    [InlineData(0, true)]
    [InlineData(-50000, false)]
    public void OpenSessionRequest_OpeningCash_Validation(decimal cash, bool expectedValid)
    {
        var request = new OpenSessionRequest(OpeningCash: cash);
        var result = _openSessionValidator.TestValidate(request);

        if (expectedValid)
            result.ShouldNotHaveAnyValidationErrors();
        else
            result.ShouldHaveValidationErrorFor(x => x.OpeningCash);
    }

    [Fact]
    public void CheckoutRequest_ValidInput_Should_Pass()
    {
        var request = new CheckoutRequest(
            Id: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            CashierSessionId: Guid.NewGuid(),
            Channel: "pos",
            Subtotal: 20000,
            DiscountTotal: 2000,
            TaxTotal: 0,
            GrandTotal: 18000,
            Items: new List<CheckoutItemRequest>
            {
                new(Guid.NewGuid(), 2, 10000, 1000)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", 18000, null)
            }
        );

        var result = _checkoutValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CheckoutRequest_GrandTotalMismatch_Should_Pass_BecauseBackendRecalculatesTotals()
    {
        var request = new CheckoutRequest(
            Id: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            CashierSessionId: Guid.NewGuid(),
            Channel: "pos",
            Subtotal: 20000,
            DiscountTotal: 2000,
            TaxTotal: 0,
            GrandTotal: 99999, // mismatch
            Items: new List<CheckoutItemRequest>
            {
                new(Guid.NewGuid(), 2, 10000, 1000)
            },
            Payments: new List<PaymentRequest>
            {
                new("cash", 18000, null)
            }
        );

        var result = _checkoutValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CheckoutRequest_NonCashWithoutReference_Should_Fail()
    {
        var request = new CheckoutRequest(
            Id: Guid.NewGuid(),
            OutletId: Guid.NewGuid(),
            CashierSessionId: Guid.NewGuid(),
            Channel: "pos",
            Subtotal: 20000,
            DiscountTotal: 0,
            TaxTotal: 0,
            GrandTotal: 20000,
            Items: new List<CheckoutItemRequest>
            {
                new(Guid.NewGuid(), 2, 10000, 0)
            },
            Payments: new List<PaymentRequest>
            {
                new("qris", 20000, null) // missing reference number for QRIS
            }
        );

        var result = _checkoutValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Payments[0].ReferenceNumber");
    }

    [Theory]
    [InlineData("ops")] // too short
    [InlineData("")] // empty
    public void VoidTransactionRequest_InvalidReason_Should_Fail(string reason)
    {
        var request = new VoidTransactionRequest(Reason: reason);
        var result = _voidValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }

    [Theory]
    [InlineData("ops")] // too short
    [InlineData("")] // empty
    public void RefundTransactionRequest_InvalidReason_Should_Fail(string reason)
    {
        var request = new RefundTransactionRequest(
            RefundMethod: "refund",
            Reason: reason,
            Items: new List<RefundTransactionItemRequest>
            {
                new(Guid.NewGuid(), 1)
            }
        );
        var result = _refundValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
