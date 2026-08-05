using System;
using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class ConsignmentItemRequestValidator : AbstractValidator<ConsignmentItemRequest>
{
    public ConsignmentItemRequestValidator()
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

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Unit price harus lebih dari 0.")
            .PrecisionScale(14, 2, false).WithMessage("Unit price maksimal 2 digit desimal dan total 14 digit.")
            .LessThanOrEqualTo(99999999999.99m).WithMessage("Unit price tidak boleh melebihi 99.999.999.999,99.")
            .GreaterThanOrEqualTo(x => x.UnitCost).WithMessage("Harga jual (UnitPrice) harus lebih besar atau sama dengan bagi hasil (UnitCost).");
    }
}

public class CreateConsignmentRequestValidator : AbstractValidator<CreateConsignmentRequest>
{
    public CreateConsignmentRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier wajib dipilih.");

        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Minimal satu item wajib diisi.")
            .Must(items => items != null && items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Produk tidak boleh duplikat dalam satu tanda terima.");

        RuleForEach(x => x.Items).SetValidator(new ConsignmentItemRequestValidator());
    }
}
