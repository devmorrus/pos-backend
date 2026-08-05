using FluentValidation;

namespace MorrusPOS.Application.Features.Products.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("Kategori wajib dipilih.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU wajib diisi.")
            .Length(3, 50).WithMessage("SKU harus berkisar antara 3 sampai 50 karakter.")
            .Matches(@"^[a-zA-Z0-9\-_]+$").WithMessage("SKU hanya boleh berisi huruf, angka, strip (-), dan underscore (_).");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama produk wajib diisi.")
            .Length(3, 150).WithMessage("Nama produk harus berkisar antara 3 sampai 150 karakter.");

        RuleFor(x => x.Barcode)
            .Matches(@"^\d{8,18}$").WithMessage("Barcode harus berupa angka dengan panjang 8 sampai 18 digit.")
            .When(x => !string.IsNullOrEmpty(x.Barcode));

        RuleFor(x => x.BasePrice)
            .GreaterThan(0).WithMessage("Harga jual harus lebih besar dari 0.");

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Harga modal harus lebih besar atau sama dengan 0.")
            .LessThan(x => x.BasePrice).WithMessage("Harga modal harus lebih rendah dari harga jual.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Satuan wajib diisi.")
            .Length(1, 20).WithMessage("Satuan harus berkisar antara 1 sampai 20 karakter.");
    }
}
