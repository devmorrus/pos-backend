using FluentValidation;

namespace MorrusPOS.Application.Features.Stock.Validators;

public sealed class StockReservationItemRequestValidator : AbstractValidator<StockReservationItemRequest>
{
    public StockReservationItemRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Qty).GreaterThan(0);
        RuleFor(x => x.BatchAllocations).Must(x => x == null || x.All(item => item.Qty > 0))
            .WithMessage("Qty alokasi batch harus lebih dari nol.");
        RuleFor(x => x.BatchAllocations).Must(x => x == null || x.Select(item => item.ProductBatchId).Distinct().Count() == x.Count)
            .WithMessage("Batch dalam satu item reservasi tidak boleh duplikat.");
        RuleFor(x => x.SerialIds).Must(x => x == null || x.Distinct().Count() == x.Count)
            .WithMessage("Serial dalam satu item reservasi tidak boleh duplikat.");
    }
}

public sealed class CreateStockReservationRequestValidator : AbstractValidator<CreateStockReservationRequest>
{
    public CreateStockReservationRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Reservation ID wajib diisi.");
        RuleFor(x => x.OutletId).NotEmpty().WithMessage("Outlet wajib diisi.");
        RuleFor(x => x.CashierSessionId).NotEmpty().WithMessage("Sesi kasir wajib diisi.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Item reservasi tidak boleh kosong.");
        RuleForEach(x => x.Items).SetValidator(new StockReservationItemRequestValidator());
        RuleFor(x => x.Items).Must(items => items
                .Select(x => (x.ProductId, x.ProductVariantId)).Distinct().Count() == items.Count)
            .WithMessage("Produk dan varian dalam reservasi tidak boleh duplikat.");
    }
}
