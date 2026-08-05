using FluentValidation;

namespace MorrusPOS.Application.Features.Suppliers.Validators;

public class UpdatePoStatusRequestValidator : AbstractValidator<UpdatePoStatusRequest>
{
    private static readonly string[] AllowedStatuses = { "pending", "completed", "cancelled" };

    public UpdatePoStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status wajib diisi.")
            .Must(s => s != null && AllowedStatuses.Contains(s.Trim().ToLowerInvariant()))
            .WithMessage("Status harus 'pending', 'completed', atau 'cancelled'.");
    }
}
