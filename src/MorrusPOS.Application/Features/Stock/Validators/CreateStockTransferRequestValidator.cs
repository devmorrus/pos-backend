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
        RuleFor(x => x.FromStockLocationId)
            .NotEmpty().WithMessage("Lokasi asal wajib diisi.");

        RuleFor(x => x.ToStockLocationId)
            .NotEmpty().WithMessage("Lokasi tujuan wajib diisi.");

        RuleFor(x => x)
            .Must(x => x.FromStockLocationId != x.ToStockLocationId)
            .WithMessage("Lokasi asal dan lokasi tujuan tidak boleh sama.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Item transfer tidak boleh kosong.");

        RuleForEach(x => x.Items)
            .SetValidator(new StockTransferItemRequestValidator());

        RuleFor(x => x.Items)
            .Must(items => items == null || items
                .Select(i => (i.ProductId, i.ProductVariantId, i.ProductBatchId))
                .Distinct().Count() == items.Count)
            .WithMessage("Baris produk, varian, dan batch transfer tidak boleh duplikat.");
    }
}
