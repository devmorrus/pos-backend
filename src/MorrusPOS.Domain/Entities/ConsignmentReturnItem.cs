using System;
using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ConsignmentReturnItem : BaseEntity
{
    // Tenant scope: row milik business ini; FK businesses via konvensi EF
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid ConsignmentReturnId { get; set; }
    public ConsignmentReturn ConsignmentReturn { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal Qty { get; set; }
}
