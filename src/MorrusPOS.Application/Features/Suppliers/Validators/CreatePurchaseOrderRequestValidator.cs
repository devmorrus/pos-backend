using FluentValidation;

namespace MorrusPOS.Application.Features.Suppliers.Validators;

public class PurchaseOrderItemRequestValidator : AbstractValidator<PurchaseOrderItemRequest>
{
    public PurchaseOrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Produk wajib dipilih.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Qty harus lebih dari 0.")
            .PrecisionScale(12, 2, false).WithMessage("Qty maksimal 2 digit desimal dan total 12 digit.")
            .LessThanOrEqualTo(99999999.99m).WithMessage("Qty tidak boleh melebihi 99.999.999,99.");

        RuleFor(x => x.UnitCost)
            .GreaterThan(0).WithMessage("Unit cost harus lebih dari 0.")
            .PrecisionScale(14, 2, false).WithMessage("Unit cost maksimal 2 digit desimal dan total 14 digit.")
            .LessThanOrEqualTo(99999999999.99m).WithMessage("Unit cost tidak boleh melebihi 99.999.999.999,99.");
    }
}

public class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    private static readonly string[] AllowedPaymentTypes = { "cash", "tempo", "consignment" };

    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier wajib dipilih.");

        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");

        RuleFor(x => x.PaymentType)
            .NotEmpty().WithMessage("Tipe pembayaran wajib diisi.")
            .Must(pt => AllowedPaymentTypes.Contains(pt.ToLowerInvariant()))
            .WithMessage("Tipe pembayaran harus 'cash', 'tempo', atau 'consignment'.");

        RuleFor(x => x.DueDate)
            .NotNull().WithMessage("Tanggal jatuh tempo wajib diisi untuk PO tempo.")
            .GreaterThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("Tanggal jatuh tempo tidak boleh di masa lalu.")
            .When(x => string.Equals(x.PaymentType, "tempo", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.DueDate)
            .Null().WithMessage("Tanggal jatuh tempo tidak boleh diisi untuk PO konsinyasi.")
            .When(x => string.Equals(x.PaymentType, "consignment", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Minimal satu item wajib diisi.")
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk tidak boleh duplikat dalam satu PO.");

        RuleForEach(x => x.Items).SetValidator(new PurchaseOrderItemRequestValidator());
    }
}
