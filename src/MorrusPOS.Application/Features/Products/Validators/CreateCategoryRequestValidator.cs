using FluentValidation;

namespace MorrusPOS.Application.Features.Products.Validators;

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama kategori wajib diisi.")
            .Length(3, 100).WithMessage("Nama kategori harus berkisar antara 3 sampai 100 karakter.");
    }
}
