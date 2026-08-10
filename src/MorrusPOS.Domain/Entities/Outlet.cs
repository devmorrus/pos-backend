using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Outlet : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();
    public ICollection<TaxRule> TaxRules { get; set; } = new List<TaxRule>();
    public ICollection<ServiceChargeRule> ServiceChargeRules { get; set; } = new List<ServiceChargeRule>();
    public ICollection<PromoCampaign> PromoCampaigns { get; set; } = new List<PromoCampaign>();
    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
