using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductAttributeValue : BaseEntity
{
    public Guid AttributeId { get; set; }
    public ProductAttribute Attribute { get; set; } = default!;

    public string Value { get; set; } = default!; // e.g., "Cokelat", "Keju", "M", "L"
}
