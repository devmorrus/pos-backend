using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Stock.Validators;

public class StockOpnameItemRequestValidator : AbstractValidator<StockOpnameItemRequest>
{
    public StockOpnameItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID wajib diisi.");

        RuleFor(x => x.PhysicalQty)
            .GreaterThanOrEqualTo(0).WithMessage("Jumlah fisik (Physical Qty) harus bernilai 0 atau lebih.");
    }
}

public class CreateStockOpnameRequestValidator : AbstractValidator<CreateStockOpnameRequest>
{
    public CreateStockOpnameRequestValidator()
    {
        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet ID wajib diisi.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Item opname tidak boleh kosong.");

        RuleForEach(x => x.Items)
            .SetValidator(new StockOpnameItemRequestValidator());

        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk opname tidak boleh duplikat.");
    }
}
