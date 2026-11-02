using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductAttributeValue : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid AttributeId { get; set; }
    public ProductAttribute Attribute { get; set; } = default!;

    public string Value { get; set; } = default!; // e.g., "Cokelat", "Keju", "M", "L"
}
