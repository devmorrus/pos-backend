using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = default!;

    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
