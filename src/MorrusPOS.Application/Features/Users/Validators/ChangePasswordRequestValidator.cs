using FluentValidation;

namespace MorrusPOS.Application.Features.Users.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Password lama wajib diisi.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password baru wajib diisi.")
            .MinimumLength(8).WithMessage("Password baru minimal 8 karakter.")
            .Matches("[A-Z]").WithMessage("Password baru harus mengandung minimal satu huruf besar (A-Z).")
            .Matches("[a-z]").WithMessage("Password baru harus mengandung minimal satu huruf kecil (a-z).")
            .Matches("[0-9]").WithMessage("Password baru harus mengandung minimal satu angka (0-9).")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password baru harus mengandung minimal satu simbol/karakter khusus.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Password baru tidak boleh sama dengan password lama.");
    }
}
