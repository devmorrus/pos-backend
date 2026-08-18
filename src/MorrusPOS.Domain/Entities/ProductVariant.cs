using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductVariant : AuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string Sku { get; set; } = default!; // e.g. ROT-COK-L
    public string? Barcode { get; set; }

    public decimal BasePrice { get; set; }
    public decimal CostPrice { get; set; }
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // Many-to-many relationship with attribute values that form this variant
    public ICollection<ProductAttributeValue> AttributeValues { get; set; } = new List<ProductAttributeValue>();
    public ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();
}
