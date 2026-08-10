using FluentValidation;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Suppliers.Validators;

public class SupplierReturnItemRequestValidator : AbstractValidator<SupplierReturnItemRequest>
{
    public SupplierReturnItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Produk retur wajib dipilih.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Qty retur harus lebih dari 0.")
            .PrecisionScale(12, 2, false).WithMessage("Qty retur maksimal 2 digit desimal dan total 12 digit.")
            .LessThanOrEqualTo(99999999.99m).WithMessage("Qty retur tidak boleh melebihi 99.999.999,99.");
    }
}

public class CreateSupplierReturnRequestValidator : AbstractValidator<CreateSupplierReturnRequest>
{
    public CreateSupplierReturnRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier wajib dipilih.");

        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty().WithMessage("Purchase order wajib dipilih.");

        RuleFor(x => x.ReturnDate)
            .NotEmpty().WithMessage("Tanggal retur wajib diisi.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Minimal satu item retur wajib diisi.")
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk retur tidak boleh duplikat.");

        RuleForEach(x => x.Items)
            .SetValidator(new SupplierReturnItemRequestValidator());
    }
}

public class UpdateSupplierReturnRequestValidator : AbstractValidator<UpdateSupplierReturnRequest>
{
    public UpdateSupplierReturnRequestValidator()
    {
        RuleFor(x => x.ReturnDate)
            .NotEmpty().WithMessage("Tanggal retur wajib diisi.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Minimal satu item retur wajib diisi.")
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk retur tidak boleh duplikat.");

        RuleForEach(x => x.Items)
            .SetValidator(new SupplierReturnItemRequestValidator());
    }
}

public class UpdateSupplierReturnStatusRequestValidator : AbstractValidator<UpdateSupplierReturnStatusRequest>
{
    private static readonly string[] AllowedStatuses =
    {
        SupplierReturnStatus.Sent,
        SupplierReturnStatus.Completed,
    };

    public UpdateSupplierReturnStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status wajib diisi.")
            .Must(status => AllowedStatuses.Contains(status.Trim().ToLowerInvariant()))
            .WithMessage("Status retur supplier harus 'sent' atau 'completed'.");
    }
}
