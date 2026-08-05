using FluentValidation;

namespace MorrusPOS.Application.Features.Transactions.Validators;

public class RefundTransactionItemRequestValidator : AbstractValidator<RefundTransactionItemRequest>
{
    public RefundTransactionItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID wajib diisi.");

        RuleFor(x => x.Qty)
            .GreaterThan(0).WithMessage("Jumlah Qty refund harus lebih besar dari 0.");
    }
}

public class RefundTransactionRequestValidator : AbstractValidator<RefundTransactionRequest>
{
    public RefundTransactionRequestValidator()
    {
        RuleFor(x => x.RefundMethod)
            .NotEmpty().WithMessage("Metode refund wajib diisi.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Alasan refund wajib diisi.")
            .Length(5, 200).WithMessage("Alasan refund harus berkisar antara 5 sampai 200 karakter.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Item refund tidak boleh kosong.");

        RuleForEach(x => x.Items)
            .SetValidator(new RefundTransactionItemRequestValidator());
    }
}
