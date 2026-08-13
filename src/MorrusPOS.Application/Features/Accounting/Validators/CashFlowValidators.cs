using FluentValidation;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Accounting.Validators;

public class CreateBusinessIncomeRequestValidator : AbstractValidator<CreateBusinessIncomeRequest>
{
    public CreateBusinessIncomeRequestValidator()
    {
        ApplyCommonRules();
    }

    private void ApplyCommonRules()
    {
        RuleFor(x => x.TrxDate)
            .NotEmpty().WithMessage("Tanggal transaksi wajib diisi.");

        RuleFor(x => x.FromChartOfAccountId)
            .NotEmpty().WithMessage("Akun asal wajib dipilih.");

        RuleFor(x => x.ToChartOfAccountId)
            .NotEmpty().WithMessage("Akun tujuan wajib dipilih.")
            .NotEqual(x => x.FromChartOfAccountId)
            .WithMessage("Akun asal dan tujuan tidak boleh sama.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Nominal transaksi harus lebih besar dari 0.")
            .PrecisionScale(14, 2, false).WithMessage("Nominal transaksi maksimal 2 digit desimal.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Catatan maksimal 500 karakter.");

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(500).WithMessage("URL lampiran maksimal 500 karakter.");
    }
}

public class CreateBusinessOutcomeRequestValidator : AbstractValidator<CreateBusinessOutcomeRequest>
{
    public CreateBusinessOutcomeRequestValidator()
    {
        RuleFor(x => x.TrxDate)
            .NotEmpty().WithMessage("Tanggal transaksi wajib diisi.");

        RuleFor(x => x.FromChartOfAccountId)
            .NotEmpty().WithMessage("Akun asal wajib dipilih.");

        RuleFor(x => x.ToChartOfAccountId)
            .NotEmpty().WithMessage("Akun tujuan wajib dipilih.")
            .NotEqual(x => x.FromChartOfAccountId)
            .WithMessage("Akun asal dan tujuan tidak boleh sama.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Nominal transaksi harus lebih besar dari 0.")
            .PrecisionScale(14, 2, false).WithMessage("Nominal transaksi maksimal 2 digit desimal.");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Catatan maksimal 500 karakter.");

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(500).WithMessage("URL lampiran maksimal 500 karakter.");
    }
}

public class CashFlowFiltersValidator : AbstractValidator<CashFlowFilters>
{
    public CashFlowFiltersValidator()
    {
        RuleFor(x => x.TrxType)
            .Must(BeValidType)
            .When(x => !string.IsNullOrWhiteSpace(x.TrxType))
            .WithMessage("Tipe transaksi harus 'in' atau 'out'.");

        RuleFor(x => x.DateTo)
            .GreaterThanOrEqualTo(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("Tanggal akhir tidak boleh sebelum tanggal mulai.");
    }

    private static bool BeValidType(string? trxType)
    {
        var normalized = trxType?.Trim().ToLowerInvariant();
        return normalized is CashFlowType.In or CashFlowType.Out;
    }
}
