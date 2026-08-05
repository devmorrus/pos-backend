using FluentValidation;

namespace MorrusPOS.Application.Features.Users.Validators;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama wajib diisi.")
            .MinimumLength(3).WithMessage("Nama minimal 3 karakter.")
            .MaximumLength(100).WithMessage("Nama maksimal 100 karakter.")
            .Matches(@"^[a-zA-Z\s\.\']+$").WithMessage("Nama hanya boleh berisi huruf, spasi, titik (.), dan tanda petik tunggal (').");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email wajib diisi.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Format email tidak valid.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password wajib diisi.")
            .MinimumLength(8).WithMessage("Password minimal 8 karakter.")
            .Matches("[A-Z]").WithMessage("Password harus mengandung minimal satu huruf besar (A-Z).")
            .Matches("[a-z]").WithMessage("Password harus mengandung minimal satu huruf kecil (a-z).")
            .Matches("[0-9]").WithMessage("Password harus mengandung minimal satu angka (0-9).")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password harus mengandung minimal satu simbol/karakter khusus.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role wajib dipilih.");
    }
}
