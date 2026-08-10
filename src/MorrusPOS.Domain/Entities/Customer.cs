using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class CustomerMemberStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}

public static class TransactionCustomerType
{
    public const string Guest = "guest";
    public const string Member = "member";
    public const string ExternalChannel = "external_channel";
}

public class Customer : AuditableEntity
{
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid? CreatedOutletId { get; set; }
    public Outlet? CreatedOutlet { get; set; }

    public string CustomerCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Gender { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsMember { get; set; } = true;
    public DateTime? JoinedAt { get; set; }
    public string MemberStatus { get; set; } = CustomerMemberStatus.Active;
    public decimal PointsBalance { get; set; }
    public decimal LifetimeSpend { get; set; }
    public DateTime? LastTransactionAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
