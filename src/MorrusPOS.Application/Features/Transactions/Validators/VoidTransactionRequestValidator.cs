using FluentValidation;

namespace MorrusPOS.Application.Features.Transactions.Validators;

public class VoidTransactionRequestValidator : AbstractValidator<VoidTransactionRequest>
{
    public VoidTransactionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Alasan void wajib diisi.")
            .Length(5, 200).WithMessage("Alasan void harus berkisar antara 5 sampai 200 karakter.");
    }
}
