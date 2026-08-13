using FluentValidation;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Accounting.Validators;

public class CreateChartOfAccountRequestValidator : AbstractValidator<CreateChartOfAccountRequest>
{
    public CreateChartOfAccountRequestValidator()
    {
        RuleFor(x => x.AccountCode)
            .NotEmpty().WithMessage("Kode akun wajib diisi.")
            .MaximumLength(30).WithMessage("Kode akun maksimal 30 karakter.");

        RuleFor(x => x.AccountName)
            .NotEmpty().WithMessage("Nama akun wajib diisi.")
            .MaximumLength(150).WithMessage("Nama akun maksimal 150 karakter.");

        RuleFor(x => x.AccountType)
            .NotEmpty().WithMessage("Tipe akun wajib dipilih.")
            .Must(BeValidAccountType).WithMessage("Tipe akun tidak valid.");
    }

    private static bool BeValidAccountType(string? accountType)
    {
        return accountType is ChartOfAccountType.Asset
            or ChartOfAccountType.Liability
            or ChartOfAccountType.Equity
            or ChartOfAccountType.Revenue
            or ChartOfAccountType.Cogs
            or ChartOfAccountType.Expense;
    }
}

public class UpdateChartOfAccountRequestValidator : AbstractValidator<UpdateChartOfAccountRequest>
{
    public UpdateChartOfAccountRequestValidator()
    {
        RuleFor(x => x.AccountCode)
            .NotEmpty().WithMessage("Kode akun wajib diisi.")
            .MaximumLength(30).WithMessage("Kode akun maksimal 30 karakter.");

        RuleFor(x => x.AccountName)
            .NotEmpty().WithMessage("Nama akun wajib diisi.")
            .MaximumLength(150).WithMessage("Nama akun maksimal 150 karakter.");

        RuleFor(x => x.AccountType)
            .NotEmpty().WithMessage("Tipe akun wajib dipilih.")
            .Must(CreateChartOfAccountRequestValidator_BeValidAccountType).WithMessage("Tipe akun tidak valid.");
    }

    private static bool CreateChartOfAccountRequestValidator_BeValidAccountType(string? accountType)
    {
        return accountType is ChartOfAccountType.Asset
            or ChartOfAccountType.Liability
            or ChartOfAccountType.Equity
            or ChartOfAccountType.Revenue
            or ChartOfAccountType.Cogs
            or ChartOfAccountType.Expense;
    }
}

public class UpdateChartOfAccountStatusRequestValidator : AbstractValidator<UpdateChartOfAccountStatusRequest>
{
    public UpdateChartOfAccountStatusRequestValidator()
    {
    }
}
