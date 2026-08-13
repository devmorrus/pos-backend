using FluentValidation;

namespace MorrusPOS.Application.Features.Reports;

public class AccountingCashFlowReportFiltersValidator : AbstractValidator<AccountingCashFlowReportFilters>
{
    public AccountingCashFlowReportFiltersValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotNull()
            .WithMessage("Tanggal mulai wajib diisi.");

        RuleFor(x => x.DateTo)
            .NotNull()
            .WithMessage("Tanggal akhir wajib diisi.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("Tanggal akhir tidak boleh sebelum tanggal mulai.");
    }
}

public class AccountingProfitLossReportFiltersValidator : AbstractValidator<AccountingProfitLossReportFilters>
{
    public AccountingProfitLossReportFiltersValidator()
    {
        RuleFor(x => x.DateFrom)
            .NotNull()
            .WithMessage("Tanggal mulai wajib diisi.");

        RuleFor(x => x.DateTo)
            .NotNull()
            .WithMessage("Tanggal akhir wajib diisi.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("Tanggal akhir tidak boleh sebelum tanggal mulai.");
    }
}
