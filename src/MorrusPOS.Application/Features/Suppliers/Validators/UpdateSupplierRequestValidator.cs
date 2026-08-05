using FluentValidation;

namespace MorrusPOS.Application.Features.Suppliers.Validators;

public class UpdateSupplierRequestValidator : AbstractValidator<UpdateSupplierRequest>
{
    private static readonly string NamePattern = @"^[a-zA-Z0-9\s\.\,\-\&\(\)\'\/]+$";
    private static readonly string PhonePattern = @"^\+?[0-9\s\-]{8,20}$";

    public UpdateSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Nama supplier wajib diisi.")
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Nama supplier wajib diisi.")
            .MinimumLength(2).WithMessage("Nama supplier minimal 2 karakter.")
            .MaximumLength(100).WithMessage("Nama supplier maksimal 100 karakter.")
            .Matches(NamePattern).WithMessage("Nama supplier hanya boleh mengandung huruf, angka, spasi, titik, koma, strip, ampersand, tanda kurung, petik satu, dan garis miring.");

        RuleFor(x => x.ContactPerson)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Nama kontak tidak boleh hanya berisi spasi.")
            .MaximumLength(100).WithMessage("Nama kontak maksimal 100 karakter.")
            .When(x => !string.IsNullOrEmpty(x.ContactPerson));

        RuleFor(x => x.Phone)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Nomor telepon tidak boleh hanya berisi spasi.")
            .Matches(PhonePattern).WithMessage("Format telepon tidak valid. Gunakan 8–20 digit, boleh mengandung spasi, strip, atau diawali +.")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Email)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Email tidak boleh hanya berisi spasi.")
            .EmailAddress().WithMessage("Format email tidak valid.")
            .MaximumLength(100).WithMessage("Email maksimal 100 karakter.")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Address)
            .Must(x => !string.IsNullOrWhiteSpace(x)).WithMessage("Alamat tidak boleh hanya berisi spasi.")
            .MaximumLength(500).WithMessage("Alamat maksimal 500 karakter.")
            .When(x => !string.IsNullOrEmpty(x.Address));
    }
}
