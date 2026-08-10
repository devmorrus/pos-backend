using FluentValidation;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Application.Features.Channels.Validators;

public class CreateChannelAccountRequestValidator : AbstractValidator<CreateChannelAccountRequest>
{
    public CreateChannelAccountRequestValidator()
    {
        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama akun channel wajib diisi.")
            .MaximumLength(100).WithMessage("Nama akun channel maksimal 100 karakter.");

        RuleFor(x => x.ChannelName)
            .NotEmpty().WithMessage("Nama platform/channel wajib diisi.")
            .MaximumLength(50).WithMessage("Nama platform/channel maksimal 50 karakter.");

        RuleFor(x => x.MerchantId)
            .MaximumLength(100).WithMessage("Merchant ID maksimal 100 karakter.");

        RuleFor(x => x.DefaultCommissionRate)
            .GreaterThanOrEqualTo(0).WithMessage("Komisi default tidak boleh negatif.")
            .LessThanOrEqualTo(100).WithMessage("Komisi default tidak boleh lebih dari 100%.")
            .PrecisionScale(5, 2, false).WithMessage("Komisi default maksimal 2 digit desimal.");
    }
}

public class UpdateChannelAccountRequestValidator : AbstractValidator<UpdateChannelAccountRequest>
{
    public UpdateChannelAccountRequestValidator()
    {
        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet wajib dipilih.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama akun channel wajib diisi.")
            .MaximumLength(100).WithMessage("Nama akun channel maksimal 100 karakter.");

        RuleFor(x => x.ChannelName)
            .NotEmpty().WithMessage("Nama platform/channel wajib diisi.")
            .MaximumLength(50).WithMessage("Nama platform/channel maksimal 50 karakter.");

        RuleFor(x => x.MerchantId)
            .MaximumLength(100).WithMessage("Merchant ID maksimal 100 karakter.");

        RuleFor(x => x.DefaultCommissionRate)
            .GreaterThanOrEqualTo(0).WithMessage("Komisi default tidak boleh negatif.")
            .LessThanOrEqualTo(100).WithMessage("Komisi default tidak boleh lebih dari 100%.")
            .PrecisionScale(5, 2, false).WithMessage("Komisi default maksimal 2 digit desimal.");
    }
}

public class CreateChannelSettlementRequestValidator : AbstractValidator<CreateChannelSettlementRequest>
{
    public CreateChannelSettlementRequestValidator()
    {
        RuleFor(x => x.ChannelAccountId)
            .NotEmpty().WithMessage("Akun channel wajib dipilih.");

        RuleFor(x => x.PeriodStartDate)
            .NotEmpty().WithMessage("Tanggal mulai periode wajib diisi.");

        RuleFor(x => x.PeriodEndDate)
            .NotEmpty().WithMessage("Tanggal akhir periode wajib diisi.")
            .GreaterThanOrEqualTo(x => x.PeriodStartDate)
            .WithMessage("Tanggal akhir periode tidak boleh sebelum tanggal mulai.");

        RuleFor(x => x.TransactionIds)
            .NotEmpty().WithMessage("Minimal satu transaksi wajib dipilih.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Transaksi settlement tidak boleh duplikat.");

        RuleFor(x => x.CommissionAmountOverride)
            .GreaterThanOrEqualTo(0).When(x => x.CommissionAmountOverride.HasValue)
            .WithMessage("Override komisi tidak boleh negatif.");
    }
}

public class UpdateChannelSettlementRequestValidator : AbstractValidator<UpdateChannelSettlementRequest>
{
    public UpdateChannelSettlementRequestValidator()
    {
        RuleFor(x => x.PeriodStartDate)
            .NotEmpty().WithMessage("Tanggal mulai periode wajib diisi.");

        RuleFor(x => x.PeriodEndDate)
            .NotEmpty().WithMessage("Tanggal akhir periode wajib diisi.")
            .GreaterThanOrEqualTo(x => x.PeriodStartDate)
            .WithMessage("Tanggal akhir periode tidak boleh sebelum tanggal mulai.");

        RuleFor(x => x.TransactionIds)
            .NotEmpty().WithMessage("Minimal satu transaksi wajib dipilih.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Transaksi settlement tidak boleh duplikat.");

        RuleFor(x => x.CommissionAmountOverride)
            .GreaterThanOrEqualTo(0).When(x => x.CommissionAmountOverride.HasValue)
            .WithMessage("Override komisi tidak boleh negatif.");
    }
}

public class UpdateChannelSettlementStatusRequestValidator : AbstractValidator<UpdateChannelSettlementStatusRequest>
{
    private static readonly string[] AllowedStatuses =
    {
        ChannelSettlementStatus.Settled,
        ChannelSettlementStatus.Cancelled,
    };

    public UpdateChannelSettlementStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status settlement wajib diisi.")
            .Must(status => AllowedStatuses.Contains(status.Trim().ToLowerInvariant()))
            .WithMessage("Status settlement harus 'settled' atau 'cancelled'.");
    }
}
