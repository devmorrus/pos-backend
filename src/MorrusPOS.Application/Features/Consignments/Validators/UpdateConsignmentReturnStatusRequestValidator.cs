using System;
using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class UpdateConsignmentReturnStatusRequestValidator : AbstractValidator<UpdateConsignmentReturnStatusRequest>
{
    private static readonly string[] AllowedStatuses = { "completed", "cancelled" };

    public UpdateConsignmentReturnStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status wajib diisi.")
            .Must(s => s != null && AllowedStatuses.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status harus 'completed' atau 'cancelled'.");
    }
}
