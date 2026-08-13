using System;
using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class ConsignmentReturnItem : BaseEntity
{
    public Guid ConsignmentReturnId { get; set; }
    public ConsignmentReturn ConsignmentReturn { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public decimal Qty { get; set; }
}
