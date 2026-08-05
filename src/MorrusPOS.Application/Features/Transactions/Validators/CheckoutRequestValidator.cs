using System.Linq;
using FluentValidation;

namespace MorrusPOS.Application.Features.Transactions.Validators;

public class CheckoutItemRequestValidator : AbstractValidator<CheckoutItemRequest>
{
    public CheckoutItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID wajib diisi.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Jumlah item (Qty) harus lebih besar dari 0.");

        RuleFor(x => x.UnitPrice)
            .GreaterThan(0).WithMessage("Harga satuan harus lebih besar dari 0.");

        RuleFor(x => x.DiscountAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Jumlah diskon harus bernilai 0 atau lebih.")
            .LessThan(x => x.UnitPrice).WithMessage("Jumlah diskon harus lebih rendah dari harga satuan.");
    }
}

public class PaymentRequestValidator : AbstractValidator<PaymentRequest>
{
    public PaymentRequestValidator()
    {
        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("Metode pembayaran wajib diisi.")
            .Must(method => new[] { "cash", "debit", "credit", "qris" }.Contains(method.ToLower()))
            .WithMessage("Metode pembayaran tidak valid. Hanya menerima cash, debit, credit, atau qris.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Nominal pembayaran harus lebih besar dari 0.");

        RuleFor(x => x.ReferenceNumber)
            .NotEmpty().WithMessage("Nomor referensi wajib diisi untuk metode pembayaran non-tunai (Debit, Kredit, QRIS).")
            .When(x => !string.IsNullOrEmpty(x.Method) && x.Method.ToLower() != "cash");
    }
}

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Transaction ID wajib diisi.");

        RuleFor(x => x.OutletId)
            .NotEmpty().WithMessage("Outlet ID wajib diisi.");

        RuleFor(x => x.CashierSessionId)
            .NotEmpty().WithMessage("Sesi kasir wajib diisi.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel transaksi wajib diisi.");

        RuleFor(x => x.Subtotal)
            .GreaterThan(0).WithMessage("Subtotal transaksi harus lebih besar dari 0.");

        RuleFor(x => x.DiscountTotal)
            .GreaterThanOrEqualTo(0).WithMessage("Total diskon harus bernilai 0 atau lebih.");

        RuleFor(x => x.TaxTotal)
            .GreaterThanOrEqualTo(0).WithMessage("Total pajak harus bernilai 0 atau lebih.");

        RuleFor(x => x.GrandTotal)
            .GreaterThan(0).WithMessage("Grand total transaksi harus lebih besar dari 0.");

        RuleFor(x => x)
            .Must(x => x.GrandTotal == (x.Subtotal - x.DiscountTotal + x.TaxTotal))
            .WithMessage("Grand total tidak cocok dengan kalkulasi (Subtotal - Diskon + Pajak).");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Keranjang belanja tidak boleh kosong.");

        RuleForEach(x => x.Items)
            .SetValidator(new CheckoutItemRequestValidator());

        RuleFor(x => x.Payments)
            .NotEmpty().WithMessage("Metode pembayaran minimal satu wajib diisi.");

        RuleForEach(x => x.Payments)
            .SetValidator(new PaymentRequestValidator());
    }
}
