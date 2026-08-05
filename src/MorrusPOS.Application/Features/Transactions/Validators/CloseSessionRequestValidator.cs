using FluentValidation;

namespace MorrusPOS.Application.Features.Transactions.Validators;

public class CloseSessionRequestValidator : AbstractValidator<CloseSessionRequest>
{
    public CloseSessionRequestValidator()
    {
        RuleFor(x => x.ActualCash)
            .GreaterThanOrEqualTo(0).WithMessage("Kas aktual harus bernilai 0 atau lebih.");
    }
}
