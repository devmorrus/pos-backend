using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Accounting.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class CashFlowRequestValidatorsTests
{
    private readonly CreateBusinessIncomeRequestValidator _incomeValidator = new();
    private readonly CreateBusinessOutcomeRequestValidator _outcomeValidator = new();

    [Fact]
    public void CreateBusinessIncomeRequest_ValidInput_Should_Pass()
    {
        var request = new CreateBusinessIncomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: null,
            FromChartOfAccountId: Guid.NewGuid(),
            ToChartOfAccountId: Guid.NewGuid(),
            Amount: 125000,
            Note: "Pendapatan harian",
            AttachmentUrl: "/uploads/cash-flows/test.pdf");

        var result = _incomeValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CreateBusinessIncomeRequest_ZeroAmount_Should_Fail()
    {
        var request = new CreateBusinessIncomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: null,
            FromChartOfAccountId: Guid.NewGuid(),
            ToChartOfAccountId: Guid.NewGuid(),
            Amount: 0,
            Note: null,
            AttachmentUrl: null);

        var result = _incomeValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void CreateBusinessOutcomeRequest_SameFromAndToAccount_Should_Fail()
    {
        var accountId = Guid.NewGuid();
        var request = new CreateBusinessOutcomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: null,
            FromChartOfAccountId: accountId,
            ToChartOfAccountId: accountId,
            Amount: 50000,
            Note: null,
            AttachmentUrl: null);

        var result = _outcomeValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.ToChartOfAccountId);
    }
}
