using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizDirectIntegrationService : IGoBizDirectIntegrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoBizApiClient _apiClient;
    private readonly IGoBizConfigProvider _configProvider;
    private readonly IOnlineStockAvailabilityService _availabilityService;

    public GoBizDirectIntegrationService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IGoBizApiClient apiClient,
        IGoBizConfigProvider configProvider,
        IOnlineStockAvailabilityService availabilityService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _apiClient = apiClient;
        _configProvider = configProvider;
        _availabilityService = availabilityService;
    }

    public async Task<GoBizDirectStatusDto> GetStatusAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        var integration = await _dbContext.GoBizDirectIntegrations.AsNoTracking().FirstOrDefaultAsync(x => x.OutletId == outletId, ct);
        return MapStatus(outletId, integration);
    }

    public async Task<GoBizDirectStatusDto> ConnectAsync(GoBizDirectConnectRequest request, CancellationToken ct = default)
    {
        var outlet = await EnsureOutletAccessibleAsync(request.OutletId, ct);
        var cfg = await _configProvider.GetByOutletAsync(request.OutletId, ct);

        var goBizOutletId = request.GoBizOutletId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(goBizOutletId))
        {
            throw new InvalidOperationException("GoBiz Outlet ID wajib diisi.");
        }

        // Opsi B: PartnerId diambil dari request (override per-outlet) atau config per-Business.
        var partnerId = string.IsNullOrWhiteSpace(request.PartnerId) ? cfg.PartnerId : request.PartnerId.Trim();
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            throw new InvalidOperationException("GoBiz Partner ID belum dikonfigurasi. Isi di menu Kredensial Client atau kirim via request.");
        }

        var integration = await _dbContext.GoBizDirectIntegrations.FirstOrDefaultAsync(x => x.OutletId == request.OutletId, ct);
        if (integration is null)
        {
            integration = new GoBizDirectIntegration
            {
                Id = Guid.NewGuid(),
                OutletId = outlet.Id,
                BusinessId = outlet.BusinessId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.GoBizDirectIntegrations.Add(integration);
        }

        integration.PartnerId = partnerId;
        integration.GoBizOutletId = goBizOutletId;
        integration.Environment = string.IsNullOrWhiteSpace(cfg.Environment) ? "Sandbox" : cfg.Environment;
        integration.IsActive = true;
        integration.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapStatus(request.OutletId, integration);
    }

    public async Task DisconnectAsync(Guid outletId, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        var integration = await _dbContext.GoBizDirectIntegrations.FirstOrDefaultAsync(x => x.OutletId == outletId, ct);
        if (integration is null) return;

        integration.IsActive = false;
        integration.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<GoBizExternalCatalogDto> GetExternalCatalogAsync(Guid outletId, CancellationToken ct = default)
    {
        var integration = await GetActiveIntegrationAsync(outletId, ct);
        var catalog = await _apiClient.GetCatalogAsync(outletId, integration.GoBizOutletId, ct);
        var pulledAt = DateTime.UtcNow;

        integration.LastCatalogPulledAtUtc = pulledAt;
        integration.UpdatedAt = pulledAt;
        await SaveLogAsync("GoBiz Catalog", "GET catalog", null, catalog.RootElement.GetRawText(), "200", true, null, ct);
        await _dbContext.SaveChangesAsync(ct);

        return new GoBizExternalCatalogDto(outletId, integration.GoBizOutletId, catalog.RootElement.Clone(), pulledAt);
    }

    public async Task<GoBizCatalogPreviewDto> PreviewCatalogAsync(Guid outletId, CancellationToken ct = default)
    {
        var integration = await GetActiveIntegrationAsync(outletId, ct);
        var payload = await BuildCatalogPayloadAsync(outletId, ct);
        return new GoBizCatalogPreviewDto(
            outletId,
            integration.GoBizOutletId,
            payload.CategoryCount,
            payload.ItemCount,
            payload.ValidationErrors,
            payload.Document.RootElement.Clone());
    }

    public async Task<GoBizCatalogSyncResultDto> SyncCatalogAsync(Guid outletId, CancellationToken ct = default)
    {
        var integration = await GetActiveIntegrationAsync(outletId, ct);
        var payload = await BuildCatalogPayloadAsync(outletId, ct);
        if (payload.ValidationErrors.Count > 0)
        {
            var message = string.Join(" ", payload.ValidationErrors.Take(3));
            integration.LastCatalogSyncStatus = "failed";
            integration.LastCatalogSyncMessage = message;
            integration.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return new GoBizCatalogSyncResultDto(outletId, integration.GoBizOutletId, false, message, payload.CategoryCount, payload.ItemCount, DateTime.UtcNow, null);
        }

        try
        {
            var response = await _apiClient.UpdateCatalogAsync(outletId, integration.GoBizOutletId, payload.Document.RootElement, ct);
            var syncedAt = DateTime.UtcNow;

            integration.LastCatalogSyncedAtUtc = syncedAt;
            integration.LastCatalogSyncStatus = "success";
            integration.LastCatalogSyncMessage = "Catalog GoFood berhasil disinkronisasi.";
            integration.UpdatedAt = syncedAt;
            await UpsertProductMappingsAsync(outletId, payload.Items, syncedAt, ct);
            await SaveLogAsync("GoBiz Catalog", "PUT catalog", payload.Document.RootElement.GetRawText(), response.RootElement.GetRawText(), "200", true, null, ct);
            await _dbContext.SaveChangesAsync(ct);

            return new GoBizCatalogSyncResultDto(outletId, integration.GoBizOutletId, true, integration.LastCatalogSyncMessage, payload.CategoryCount, payload.ItemCount, syncedAt, response.RootElement.Clone());
        }
        catch (Exception ex)
        {
            integration.LastCatalogSyncStatus = "failed";
            integration.LastCatalogSyncMessage = ex.Message;
            integration.UpdatedAt = DateTime.UtcNow;
            await SaveLogAsync("GoBiz Catalog", "PUT catalog", payload.Document.RootElement.GetRawText(), null, null, false, ex.Message, ct);
            await _dbContext.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<GoBizIntegrationLogDto>> GetLogsAsync(Guid outletId, int take, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        return await _dbContext.IntegrationLogs
            .AsNoTracking()
            .Where(x => x.ServiceName.StartsWith("GoBiz"))
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new GoBizIntegrationLogDto(x.Id, x.ServiceName, x.StatusCode, x.IsSuccess, x.ErrorMessage, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GoBizOrderInboxDto>> GetOrderInboxesAsync(Guid outletId, int take, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        return await _dbContext.GoBizOrderInboxes
            .AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.ReceivedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new GoBizOrderInboxDto(x.Id, x.GoBizOrderId, x.OutletId, x.EventType, x.Status, x.TransactionId, x.ErrorMessage, x.ReceivedAtUtc, x.ProcessedAtUtc))
            .ToListAsync(ct);
    }

    private async Task<CatalogPayload> BuildCatalogPayloadAsync(Guid outletId, CancellationToken ct)
    {
        // Opsi B: filter produk by Business milik outlet (bukan semata claim user),
        // agar tidak 0/0 saat Owner switch outlet / token lama tanpa business_id.
        var outlet = await _dbContext.Outlets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == outletId, ct)
            ?? throw new InvalidOperationException("Outlet tidak valid.");
        var businessId = outlet.BusinessId ?? _currentUserService.BusinessId;

        var query = _dbContext.Products
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.ModifierGroups).ThenInclude(g => g.Options)
            .Where(p => p.IsActive && !p.IsRawMaterial);
        if (businessId.HasValue)
            query = query.Where(p => p.BusinessId == businessId.Value);

        var products = await query
            .OrderBy(p => p.Category!.Name).ThenBy(p => p.Name)
            .ToListAsync(ct);

        // Buffer stock: ketersediaan online = max(0, QtyOnHand - BufferQty).
        // Jika tidak ada policy, buffer dianggap 0 (fallback ke stok fisik).
        var availabilityMap = await _availabilityService.GetAvailabilityMapAsync(outletId, ct);

        static decimal GetOnlineQty(
            IReadOnlyDictionary<OnlineStockKey, OnlineStockAvailabilityDto> map,
            Guid productId,
            Guid? variantId)
        {
            if (map.TryGetValue(new OnlineStockKey(productId, variantId), out var exact))
            {
                return exact.AvailableOnlineQty;
            }

            // Fallback policy produk induk untuk varian (konsisten dengan BufferStockService).
            if (variantId.HasValue && map.TryGetValue(new OnlineStockKey(productId, null), out var parent))
            {
                return parent.AvailableOnlineQty;
            }

            return 0;
        }

        var errors = new List<string>();
        var duplicateSkus = products
            .SelectMany(p => p.HasVariants && p.Variants.Count > 0 ? p.Variants.Select(v => v.Sku) : [p.Sku])
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .GroupBy(sku => sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSkus.Count > 0)
        {
            errors.Add($"SKU duplikat untuk sync GoBiz: {string.Join(", ", duplicateSkus.Take(5))}.");
        }

        var categoryGroups = products
            .Select(p => p.Category)
            .Where(c => c != null)
            .GroupBy(c => c!.Id)
            .ToList();

        var duplicateCategoryNames = categoryGroups
            .Select(g => TruncateText(g.First()!.Name, 150) ?? string.Empty)
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var menus = categoryGroups
            .Select(g =>
            {
                var category = g.First()!;
                var menuId = StableId("menu", category.Id.ToString());
                var baseName = TruncateText(category.Name, 150) ?? string.Empty;
                var uniqueName = duplicateCategoryNames.Contains(baseName)
                    ? TruncateText($"{baseName} ({menuId[..8]})", 150) ?? baseName
                    : baseName;

                return new MenuPayload(menuId, uniqueName, new List<MenuItemPayload>());
            })
            .ToList();

        var menusById = menus.ToDictionary(x => x.ExternalId, x => x.Items);
        var variantCategories = new Dictionary<string, VariantCategoryPayload>(StringComparer.OrdinalIgnoreCase);
        var mappedItems = new List<MappedCatalogItem>();

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.Sku) && (!product.HasVariants || product.Variants.Count == 0))
            {
                errors.Add($"Produk {product.Name} belum memiliki SKU.");
                continue;
            }

            if (product.BasePrice <= 0 && (!product.HasVariants || product.Variants.Count == 0))
            {
                errors.Add($"Produk {product.Name} belum memiliki harga jual valid.");
                continue;
            }

            var menuId = StableId("menu", product.CategoryId.ToString());
            if (!menusById.ContainsKey(menuId))
            {
                errors.Add($"Produk {product.Name} kategorinya tidak valid/tidak aktif.");
                continue;
            }
            var isAvailable = GetOnlineQty(availabilityMap, product.Id, null) > 0;
            var variantCategoryIds = new List<string>();

            if (product.HasVariants && product.Variants.Count > 0)
            {
                var variantCategoryExternalId = StableId("vc", product.Id.ToString());
                variantCategoryIds.Add(variantCategoryExternalId);

                if (!variantCategories.ContainsKey(variantCategoryExternalId))
                {
                    var required = product.ModifierGroups.Any(g => g.IsRequired);
                    var maxSelection = product.ModifierGroups.Any() ? Math.Max(1, product.ModifierGroups.Max(g => g.MaxSelection)) : 1;
                    variantCategories[variantCategoryExternalId] = new VariantCategoryPayload(
                        variantCategoryExternalId,
                        TruncateText(product.Name, 50) ?? product.Name,
                        TruncateText(product.Name, 50) ?? product.Name,
                        required ? 1 : 0,
                        maxSelection,
                        new List<VariantPayload>());
                }

                foreach (var variant in product.Variants.Where(v => v.IsActive))
                {
                    if (string.IsNullOrWhiteSpace(variant.Sku))
                    {
                        errors.Add($"Varian produk {product.Name} belum memiliki SKU.");
                        continue;
                    }

                    if (variant.BasePrice <= 0)
                    {
                        errors.Add($"Varian produk {product.Name} SKU {variant.Sku} belum memiliki harga jual valid.");
                        continue;
                    }

                    // Jangan fallback ke stok parent: varian hanya tersedia
                    // jika stok online varian itu sendiri > 0.
                    var variantInStock = GetOnlineQty(availabilityMap, product.Id, variant.Id) > 0;
                    var itemId = StableId("item", variant.Sku);
                    var item = CreateItem(
                        itemId,
                        product.Name,
                        TruncateText($"{product.Name} {variant.Sku}", 200) ?? product.Name,
                        variant.BasePrice,
                        variant.ImageUrl ?? product.ImageUrl,
                        variantInStock,
                        variantCategoryIds,
                        product.ModifierGroups);
                    menusById[menuId].Add(item);
                    mappedItems.Add(new MappedCatalogItem(product.Id, variant.Id, itemId, menuId));

                    var vc = variantCategories[variantCategoryExternalId];
                    vc.Variants.Add(new VariantPayload(
                        StableId("var", variant.Sku),
                        TruncateText(variant.Sku, 50) ?? variant.Sku,
                        0,
                        variantInStock));
                }
            }
            else
            {
                var itemId = StableId("item", product.Sku);
                var item = CreateItem(
                    itemId,
                    product.Name,
                    TruncateText(product.Name, 200) ?? product.Name,
                    product.BasePrice,
                    product.ImageUrl,
                    isAvailable,
                    variantCategoryIds,
                    product.ModifierGroups);
                menusById[menuId].Add(item);
                mappedItems.Add(new MappedCatalogItem(product.Id, null, itemId, menuId));
            }
        }

        // Jangan kirim menu kosong ke GoBiz (penyebab 422 "menus: missing required field").
        // Hanya menu yang punya item yang dikirim; kategorinya dihitung dari yang terkirim.
        var shippableMenus = menus.Where(m => m.Items.Count > 0).ToList();
        if (products.Count == 0)
        {
            errors.Add("Tidak ada produk aktif untuk Business/outlet ini. Tambahkan produk + kategori + SKU + harga dulu.");
        }
        else if (mappedItems.Count == 0)
        {
            if (errors.Count == 0)
                errors.Add("Semua produk terfilter (SKU/harga/kategori belum valid). Periksa Validation di Preview.");
        }
        else if (shippableMenus.Count == 0)
        {
            errors.Add("Semua menu kosong setelah mapping. Pastikan tiap kategori punya minimal 1 item valid.");
        }

        var payload = new
        {
            request_id = Guid.NewGuid().ToString(),
            menus = shippableMenus.Select(menu => new
            {
                external_id = menu.ExternalId,
                name = menu.Name,
                menu_items = menu.Items.Select(item => new
                {
                    external_id = item.ExternalId,
                    name = item.Name,
                    internal_name = item.InternalName,
                    description = item.Description,
                    in_stock = item.InStock,
                    price = item.Price,
                    image = item.Image,
                    operational_hours = item.OperationalHours,
                    variant_category_external_ids = item.VariantCategoryExternalIds
                }).ToList()
            }).ToList(),
            variant_categories = variantCategories.Values.Select(category => new
            {
                external_id = category.ExternalId,
                internal_name = category.InternalName,
                name = category.Name,
                rules = new
                {
                    selection = new
                    {
                        min_quantity = category.MinQuantity,
                        max_quantity = category.MaxQuantity
                    }
                },
                variants = category.Variants.Select(variant => new
                {
                    external_id = variant.ExternalId,
                    name = variant.Name,
                    price = variant.Price,
                    in_stock = variant.InStock
                }).ToList()
            }).ToList()
        };

        var document = JsonDocument.Parse(JsonSerializer.Serialize(payload, JsonOptions));
        return new CatalogPayload(document, shippableMenus.Count, mappedItems.Count, errors, mappedItems);
    }

    private static MenuItemPayload CreateItem(
        string externalId,
        string name,
        string internalName,
        decimal price,
        string? imageUrl,
        bool inStock,
        IReadOnlyList<string> variantCategoryExternalIds,
        ICollection<ModifierGroup> modifierGroups)
        => new(
            externalId,
            TruncateText(name, 150) ?? name,
            TruncateText(internalName, 200) ?? internalName,
            TruncateText(modifierGroups.Any() ? string.Join(", ", modifierGroups.Select(g => g.Name)) : null, 200),
            inStock,
            decimal.ToInt64(decimal.Round(price, 0)),
            NormalizeImageUrl(imageUrl),
            BuildOperationalHours(),
            variantCategoryExternalIds.ToList());

    private async Task UpsertProductMappingsAsync(Guid outletId, IReadOnlyList<MappedCatalogItem> items, DateTime syncedAt, CancellationToken ct)
    {
        var existing = await _dbContext.GoBizProductMappings.Where(x => x.OutletId == outletId).ToListAsync(ct);
        foreach (var item in items)
        {
            var mapping = existing.FirstOrDefault(x => x.ProductId == item.ProductId && x.ProductVariantId == item.ProductVariantId);
            if (mapping is null)
            {
                mapping = new GoBizProductMapping
                {
                    Id = Guid.NewGuid(),
                    OutletId = outletId,
                    ProductId = item.ProductId,
                    ProductVariantId = item.ProductVariantId,
                    CreatedAt = syncedAt
                };
                _dbContext.GoBizProductMappings.Add(mapping);
            }

            mapping.GoBizItemId = item.GoBizItemId;
            mapping.GoBizCategoryId = item.GoBizCategoryId;
            mapping.IsSynced = true;
            mapping.LastSyncedAtUtc = syncedAt;
            mapping.LastSyncError = null;
            mapping.UpdatedAt = syncedAt;
        }
    }

    private async Task<GoBizDirectIntegration> GetActiveIntegrationAsync(Guid outletId, CancellationToken ct)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        var integration = await _dbContext.GoBizDirectIntegrations.FirstOrDefaultAsync(x => x.OutletId == outletId, ct);
        if (integration is null || !integration.IsActive)
        {
            throw new InvalidOperationException("Outlet belum terhubung ke GoBiz direct integration.");
        }

        return integration;
    }

    private async Task<Outlet> EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == outletId, ct);
        if (outlet == null || !outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak valid atau tidak aktif.");
        }

        if (_currentUserService.IsAuthenticated && _currentUserService.Role != "Owner" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return outlet;
    }

    private async Task SaveLogAsync(string serviceName, string operation, string? request, string? response, string? statusCode, bool success, string? error, CancellationToken ct)
    {
        _dbContext.IntegrationLogs.Add(new IntegrationLog
        {
            Id = Guid.NewGuid(),
            ServiceName = $"{serviceName} - {operation}",
            RequestPayload = Truncate(request),
            ResponsePayload = Truncate(response),
            StatusCode = statusCode,
            IsSuccess = success,
            ErrorMessage = error,
            CreatedAt = DateTime.UtcNow
        });

        await Task.CompletedTask;
    }

    private static string StableId(string prefix, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return $"{prefix}_{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }

    private static string? TruncateText(string? value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? value
            : value.Length <= maxLength ? value : value[..maxLength];

    private static string? NormalizeImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : null;
    }

    private static object BuildOperationalHours()
        => new
        {
            sunday = new[] { new { start = "00:00", end = "23:59" } },
            monday = new[] { new { start = "00:00", end = "23:59" } },
            tuesday = new[] { new { start = "00:00", end = "23:59" } },
            wednesday = new[] { new { start = "00:00", end = "23:59" } },
            thursday = new[] { new { start = "00:00", end = "23:59" } },
            friday = new[] { new { start = "00:00", end = "23:59" } },
            saturday = new[] { new { start = "00:00", end = "23:59" } }
        };

    private static string? Truncate(string? value)
        => string.IsNullOrWhiteSpace(value) ? value : value.Length <= 8000 ? value : value[..8000];

    private static GoBizDirectStatusDto MapStatus(Guid outletId, GoBizDirectIntegration? integration)
        => new(
            outletId,
            integration?.GoBizOutletId,
            integration?.Environment ?? "Sandbox",
            integration != null,
            integration?.IsActive ?? false,
            integration?.LastCatalogPulledAtUtc,
            integration?.LastCatalogSyncedAtUtc,
            integration?.LastCatalogSyncStatus,
            integration?.LastCatalogSyncMessage,
            integration?.LastWebhookAtUtc);

    private sealed record CatalogPayload(JsonDocument Document, int CategoryCount, int ItemCount, IReadOnlyList<string> ValidationErrors, IReadOnlyList<MappedCatalogItem> Items);
    private sealed record MappedCatalogItem(Guid ProductId, Guid? ProductVariantId, string GoBizItemId, string GoBizCategoryId);
    private sealed record MenuPayload(string ExternalId, string Name, List<MenuItemPayload> Items);
    private sealed record MenuItemPayload(string ExternalId, string Name, string InternalName, string? Description, bool InStock, long Price, string? Image, object OperationalHours, List<string> VariantCategoryExternalIds);
    private sealed record VariantCategoryPayload(string ExternalId, string InternalName, string Name, int MinQuantity, int MaxQuantity, List<VariantPayload> Variants);
    private sealed record VariantPayload(string ExternalId, string Name, long Price, bool InStock);
}
