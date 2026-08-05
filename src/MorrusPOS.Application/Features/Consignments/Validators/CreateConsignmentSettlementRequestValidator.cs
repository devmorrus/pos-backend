using System;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class CreateConsignmentSettlementRequestValidator : AbstractValidator<CreateConsignmentSettlementRequest>
{
    public CreateConsignmentSettlementRequestValidator()
    {
        RuleFor(x => x.SupplierId)
            .NotEmpty().WithMessage("Supplier wajib dipilih.");

        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");
    }
}
