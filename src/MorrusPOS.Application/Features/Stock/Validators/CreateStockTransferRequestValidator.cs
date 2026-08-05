using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Stock.Validators;

public class StockTransferItemRequestValidator : AbstractValidator<StockTransferItemRequest>
{
    public StockTransferItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID wajib diisi.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Jumlah transfer (Qty) harus lebih besar dari 0.");
    }
}

public class CreateStockTransferRequestValidator : AbstractValidator<CreateStockTransferRequest>
{
    public CreateStockTransferRequestValidator()
    {
        RuleFor(x => x.FromOutletId)
            .NotEmpty().WithMessage("Outlet asal wajib diisi.");

        RuleFor(x => x.ToOutletId)
            .NotEmpty().WithMessage("Outlet tujuan wajib diisi.");

        RuleFor(x => x)
            .Must(x => x.FromOutletId != x.ToOutletId)
            .WithMessage("Outlet asal dan outlet tujuan tidak boleh sama.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Item transfer tidak boleh kosong.");

        RuleForEach(x => x.Items)
            .SetValidator(new StockTransferItemRequestValidator());

        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk transfer tidak boleh duplikat.");
    }
}
