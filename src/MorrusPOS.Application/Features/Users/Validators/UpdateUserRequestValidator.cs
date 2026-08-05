using FluentValidation;

namespace MorrusPOS.Application.Features.Users.Validators;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama wajib diisi.")
            .MinimumLength(3).WithMessage("Nama minimal 3 karakter.")
            .MaximumLength(100).WithMessage("Nama maksimal 100 karakter.")
            .Matches(@"^[a-zA-Z\s\.\']+$").WithMessage("Nama hanya boleh berisi huruf, spasi, titik (.), dan tanda petik tunggal (').");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email wajib diisi.")
            .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithMessage("Format email tidak valid.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role wajib dipilih.");
    }
}
