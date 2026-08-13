using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductRecipe : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    // The raw material/ingredient (which is also modeled as a Product, but with IsRawMaterial = true)
    public Guid RawMaterialProductId { get; set; }
    public Product RawMaterialProduct { get; set; } = default!;

    public decimal QuantityRequired { get; set; } // e.g. 0.15 kg of flour
}
