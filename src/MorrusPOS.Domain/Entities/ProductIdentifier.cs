using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

// Maintained by database triggers, including writes from older clients/imports.
public class ProductIdentifier : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public string IdentifierType { get; set; } = "";
    public string NormalizedValue { get; set; } = "";
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
}
