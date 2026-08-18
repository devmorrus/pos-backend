using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductAttribute : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!; // e.g., "Size", "Flavor", "Color"
    public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
}
