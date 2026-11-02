using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ModifierGroup : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Name { get; set; } = default!; // e.g. "Toppings", "Packaging"
    public bool IsRequired { get; set; } = false;
    public int MinSelection { get; set; } = 0;
    public int MaxSelection { get; set; } = 1;

    public ICollection<ModifierOption> Options { get; set; } = new List<ModifierOption>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class ModifierOption : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid ModifierGroupId { get; set; }
    public ModifierGroup ModifierGroup { get; set; } = default!;

    public string Name { get; set; } = default!; // e.g. "Extra Cheese", "Paper Bag"
    public decimal ExtraPrice { get; set; }
    public decimal ExtraCost { get; set; }
}

public class ModifierGroupProduct
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    // Names preserve the existing implicit EF join table schema.
    public Guid ModifierGroupsId { get; set; }
    public ModifierGroup ModifierGroup { get; set; } = default!;
    public Guid ProductsId { get; set; }
    public Product Product { get; set; } = default!;
}
