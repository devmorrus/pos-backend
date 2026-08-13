using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Reports;
using Xunit;

namespace MorrusPOS.UnitTests;

public class AccountingReportValidatorsTests
{
    private readonly AccountingCashFlowReportFiltersValidator _cashFlowValidator = new();
    private readonly AccountingProfitLossReportFiltersValidator _profitLossValidator = new();

    [Fact]
    public void AccountingCashFlowReportFilters_ValidRange_Should_Pass()
    {
        var filters = new AccountingCashFlowReportFilters(
            DateFrom: new DateTime(2026, 8, 1),
            DateTo: new DateTime(2026, 8, 13),
            OutletId: null,
            ChartOfAccountId: null,
            Keyword: null);

        var result = _cashFlowValidator.TestValidate(filters);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void AccountingCashFlowReportFilters_EndBeforeStart_Should_Fail()
    {
        var filters = new AccountingCashFlowReportFilters(
            DateFrom: new DateTime(2026, 8, 13),
            DateTo: new DateTime(2026, 8, 1),
            OutletId: null,
            ChartOfAccountId: null,
            Keyword: null);

        var result = _cashFlowValidator.TestValidate(filters);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }

    [Fact]
    public void AccountingProfitLossReportFilters_MissingDates_Should_Fail()
    {
        var filters = new AccountingProfitLossReportFilters(
            DateFrom: null,
            DateTo: null,
            OutletId: null,
            Keyword: null);

        var result = _profitLossValidator.TestValidate(filters);
        result.ShouldHaveValidationErrorFor(x => x.DateFrom);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }
}
