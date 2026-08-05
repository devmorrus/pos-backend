using System;
using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class ConsignmentReturnItemRequestValidator : AbstractValidator<ConsignmentReturnItemRequest>
{
    public ConsignmentReturnItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Produk wajib dipilih.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Qty harus lebih dari 0.")
            .PrecisionScale(12, 2, false).WithMessage("Qty maksimal 2 digit desimal dan total 12 digit.")
            .LessThanOrEqualTo(99999999.99m).WithMessage("Qty tidak boleh melebihi 99.999.999,99.");
    }
}

public class CreateConsignmentReturnRequestValidator : AbstractValidator<CreateConsignmentReturnRequest>
{
    public CreateConsignmentReturnRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier wajib dipilih.");

        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Minimal satu item wajib diisi.")
            .Must(items => items != null && items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk tidak boleh duplikat dalam satu dokumen retur.");

        RuleForEach(x => x.Items).SetValidator(new ConsignmentReturnItemRequestValidator());
    }
}
