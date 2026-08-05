using FluentValidation;

namespace MorrusPOS.Application.Features.Transactions.Validators;

public class OpenSessionRequestValidator : AbstractValidator<OpenSessionRequest>
{
    public OpenSessionRequestValidator()
    {
        RuleFor(x => x.OpeningCash)
            .GreaterThanOrEqualTo(0).WithMessage("Kas awal harus bernilai 0 atau lebih.");
    }
}
