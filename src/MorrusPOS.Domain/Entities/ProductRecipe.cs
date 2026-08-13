using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductRecipe : BaseEntity
{
    // The finished product variant
    public Guid ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;

    // The raw material/ingredient (which is also modeled as a Product, but with IsRawMaterial = true)
    public Guid RawMaterialProductId { get; set; }
    public Product RawMaterialProduct { get; set; } = default!;

    public decimal QuantityRequired { get; set; } // e.g. 0.15 kg of flour
}
