using System;
using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class UpdateConsignmentSettlementStatusRequestValidator : AbstractValidator<UpdateConsignmentSettlementStatusRequest>
{
    private static readonly string[] AllowedStatuses = { "settled", "cancelled" };

    public UpdateConsignmentSettlementStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status wajib diisi.")
            .Must(s => s != null && AllowedStatuses.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status harus 'settled' atau 'cancelled'.");
    }
}
