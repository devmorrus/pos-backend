using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class ProductTrackingModes
{
    public const string None = "none";
    public const string Quantity = "quantity";
    public const string Batch = "batch";
    public const string Serial = "serial";

    public static readonly string[] All = [None, Quantity, Batch, Serial];
}

public class Product : AuditableEntity
{
    public Guid? BrandId { get; set; }
    public ProductBrand? Brand { get; set; }
    public Guid? ModelId { get; set; }
    public ProductModel? Model { get; set; }
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = default!;

    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Barcode { get; set; }
    public decimal BasePrice { get; set; }
    public decimal CostPrice { get; set; }
    public string Unit { get; set; } = default!; // pcs, kg, dus, dll
    public bool IsConsignment { get; set; } = false;
    public bool HasVariants { get; set; } = false;
    public bool IsRawMaterial { get; set; } = false;
    public string? ImageUrl { get; set; }
    public bool? IsTaxable { get; set; }
    public bool? IsServiceChargeable { get; set; }
    public bool IsActive { get; set; } = true;
    public string TrackingMode { get; set; } = ProductTrackingModes.Quantity;
    public uint Version { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ModifierGroup> ModifierGroups { get; set; } = new List<ModifierGroup>();
    public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    public ICollection<InventoryStock> InventoryStocks { get; set; } = new List<InventoryStock>();
    public ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();
    public ICollection<ProductRecipe> Recipes { get; set; } = new List<ProductRecipe>();
}
