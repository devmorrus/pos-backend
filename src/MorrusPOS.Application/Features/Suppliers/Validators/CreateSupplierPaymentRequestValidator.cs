using FluentValidation;

namespace MorrusPOS.Application.Features.Suppliers.Validators;

public class CreateSupplierPaymentRequestValidator : AbstractValidator<CreateSupplierPaymentRequest>
{
    private static readonly string RefNumberPattern = @"^[a-zA-Z0-9\-\.\/]+$";

    public CreateSupplierPaymentRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId)
            .NotEmpty().WithMessage("Purchase Order wajib dipilih.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Nominal pembayaran harus lebih dari 0.")
            .PrecisionScale(14, 2, false).WithMessage("Nominal pembayaran maksimal 2 digit desimal dan total 14 digit.")
            .LessThanOrEqualTo(99999999999.99m).WithMessage("Nominal pembayaran tidak boleh melebihi 99.999.999.999,99.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Metode pembayaran wajib diisi.")
            .MinimumLength(2).WithMessage("Metode pembayaran minimal 2 karakter.")
            .MaximumLength(30).WithMessage("Metode pembayaran maksimal 30 karakter.");

        RuleFor(x => x.ReferenceNumber)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Reference number tidak boleh hanya berisi spasi.")
            .MaximumLength(100).WithMessage("Reference number maksimal 100 karakter.")
            .Matches(RefNumberPattern).WithMessage("Reference number hanya boleh mengandung huruf, angka, strip, titik, dan garis miring.")
            .When(x => !string.IsNullOrEmpty(x.ReferenceNumber));
    }
}
