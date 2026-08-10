using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Product : AuditableEntity
{
    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Barcode { get; set; }
    public decimal BasePrice { get; set; }
    public decimal CostPrice { get; set; }
    public string Unit { get; set; } = default!; // pcs, kg, dus, dll
    public bool IsConsignment { get; set; } = false;
    public string? ImageUrl { get; set; }
    public bool? IsTaxable { get; set; }
    public bool? IsServiceChargeable { get; set; }
    public bool IsActive { get; set; } = true;
    public uint Version { get; set; }

    public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    public ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();
    public ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();
}
