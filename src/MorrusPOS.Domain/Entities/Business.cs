using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Business : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!; // e.g. F&B, Retail, Services
    public string? Phone { get; set; }

    // SaaS Subscription Fields
    public string SubscriptionStatus { get; set; } = "Trial"; // Trial, Active, Expired, Locked
    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;
    public DateTime TrialEndDate { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? SubscriptionEndDate { get; set; }
    public string? SelectedPackage { get; set; } // Free Trial, Basic, Premium

    // Track the primary Owner user
    public Guid? OwnerId { get; set; }

    // Navigation Properties
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Outlet> Outlets { get; set; } = new List<Outlet>();
    public ICollection<ChartOfAccount> ChartOfAccounts { get; set; } = new List<ChartOfAccount>();
    public ICollection<CashFlow> CashFlows { get; set; } = new List<CashFlow>();
    public ICollection<AccountTransaction> AccountTransactions { get; set; } = new List<AccountTransaction>();
}
