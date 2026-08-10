using FluentValidation;

namespace MorrusPOS.Application.Features.Customers.Validators;

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama customer wajib diisi.")
            .MaximumLength(150).WithMessage("Nama customer maksimal 150 karakter.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Nomor HP wajib diisi.")
            .MaximumLength(30).WithMessage("Nomor HP maksimal 30 karakter.");

        RuleFor(x => x.Email)
            .MaximumLength(150).WithMessage("Email maksimal 150 karakter.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Format email tidak valid.");

        RuleFor(x => x.Gender)
            .MaximumLength(20).WithMessage("Gender maksimal 20 karakter.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Catatan maksimal 500 karakter.");
    }
}

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama customer wajib diisi.")
            .MaximumLength(150).WithMessage("Nama customer maksimal 150 karakter.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Nomor HP wajib diisi.")
            .MaximumLength(30).WithMessage("Nomor HP maksimal 30 karakter.");

        RuleFor(x => x.Email)
            .MaximumLength(150).WithMessage("Email maksimal 150 karakter.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Format email tidak valid.");

        RuleFor(x => x.Gender)
            .MaximumLength(20).WithMessage("Gender maksimal 20 karakter.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Catatan maksimal 500 karakter.");
    }
}
