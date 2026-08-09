using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Supplier : AuditableEntity
{
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
