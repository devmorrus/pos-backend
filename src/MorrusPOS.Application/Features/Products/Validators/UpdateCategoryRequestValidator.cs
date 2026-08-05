using FluentValidation;

namespace MorrusPOS.Application.Features.Products.Validators;

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama kategori wajib diisi.")
            .Length(3, 100).WithMessage("Nama kategori harus berkisar antara 3 sampai 100 karakter.");
    }
}
