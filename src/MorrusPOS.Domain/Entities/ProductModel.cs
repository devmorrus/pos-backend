using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ProductModel : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid BrandId { get; set; }
    public ProductBrand Brand { get; set; } = default!;
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
