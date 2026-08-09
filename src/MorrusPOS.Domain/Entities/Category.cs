using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Category : AuditableEntity
{
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
