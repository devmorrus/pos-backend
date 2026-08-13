using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Accounting.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class AccountingRequestValidatorsTests
{
    private readonly CreateChartOfAccountRequestValidator _createValidator = new();
    private readonly UpdateChartOfAccountRequestValidator _updateValidator = new();

    [Fact]
    public void CreateChartOfAccountRequest_ValidInput_Should_Pass()
    {
        var request = new CreateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "Kas Utama",
            AccountType: "asset",
            IsCashBank: true,
            OutletId: null,
            ParentAccountId: null);

        var result = _createValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("other")]
    public void CreateChartOfAccountRequest_InvalidAccountType_Should_Fail(string accountType)
    {
        var request = new CreateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "Kas Utama",
            AccountType: accountType,
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: null);

        var result = _createValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AccountType);
    }

    [Fact]
    public void UpdateChartOfAccountRequest_EmptyName_Should_Fail()
    {
        var request = new UpdateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "",
            AccountType: "asset",
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: null,
            IsActive: true);

        var result = _updateValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AccountName);
    }
}
