using FluentValidation;

namespace MorrusPOS.Application.Features.Stock.Validators;

public class UpsertBufferStockRequestValidator : AbstractValidator<UpsertBufferStockRequest>
{
    public UpsertBufferStockRequestValidator()
    {
        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet ID wajib diisi.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID wajib diisi.");

        RuleFor(x => x.BufferQty)
            .GreaterThanOrEqualTo(0).WithMessage("Buffer stock harus bernilai 0 atau lebih.")
            .LessThanOrEqualTo(999999).WithMessage("Buffer stock terlalu besar.");

        // Batasi 2 angka desimal agar konsisten dengan decimal(12,2)
        RuleFor(x => x.BufferQty)
            .Must(qty => decimal.Round(qty, 2) == qty)
            .WithMessage("Buffer stock maksimal 2 angka desimal.");
    }
}
