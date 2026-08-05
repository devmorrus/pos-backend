using System;
using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Consignments.Validators;

public class UpdateConsignmentStatusRequestValidator : AbstractValidator<UpdateConsignmentStatusRequest>
{
    private static readonly string[] AllowedStatuses = { "received", "cancelled" };

    public UpdateConsignmentStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status wajib diisi.")
            .Must(s => s != null && AllowedStatuses.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status harus 'received' atau 'cancelled'.");
    }
}
