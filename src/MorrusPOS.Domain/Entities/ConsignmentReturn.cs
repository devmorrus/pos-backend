using System;
using System.Collections.Generic;
using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ConsignmentReturnStatus
{
    public const string Draft = "draft";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}

public class ConsignmentReturn : AuditableEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = default!;

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string ReturnNumber { get; set; } = default!;
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = ConsignmentReturnStatus.Draft;

    public Guid CreatedBy { get; set; }
    public User CreatedByUser { get; set; } = default!;

    public ICollection<ConsignmentReturnItem> Items { get; set; } = new List<ConsignmentReturnItem>();
}
