using FluentValidation;

namespace MorrusPOS.Application.Features.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email wajib diisi.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Format email tidak valid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password wajib diisi.");
    }
}
