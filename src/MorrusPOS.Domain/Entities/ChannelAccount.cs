using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ChannelAccount : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string ChannelName { get; set; } = default!; // e.g. "GoFood", "GrabFood", "ShopeeFood"
    public string MerchantId { get; set; } = default!;
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
}
