using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizOrderWebhookService : IGoBizOrderWebhookService
{
    private readonly AppDbContext _dbContext;
    private readonly IGoBizConfigProvider _configProvider;
    private readonly ILogger<GoBizOrderWebhookService> _logger;

    public GoBizOrderWebhookService(
        AppDbContext dbContext,
        IGoBizConfigProvider configProvider,
        ILogger<GoBizOrderWebhookService> logger)
    {
        _dbContext = dbContext;
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task ProcessWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new InvalidOperationException("Payload webhook GoBiz kosong.");
        }

        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;
        var goBizOutletId = ReadString(root, "outlet_id") ?? ReadString(root, "outletId") ?? ReadString(root, "merchant_outlet_id");
        var orderId = ReadString(root, "order_id") ?? ReadString(root, "orderId") ?? ReadString(root, "id");
        var eventType = ReadString(root, "event_type") ?? ReadString(root, "eventType") ?? ReadString(root, "type") ?? "order";

        if (string.IsNullOrWhiteSpace(goBizOutletId))
        {
            throw new InvalidOperationException("Outlet ID GoBiz tidak ditemukan pada webhook.");
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new InvalidOperationException("Order ID GoBiz tidak ditemukan pada webhook.");
        }

        var integration = await _dbContext.GoBizDirectIntegrations.FirstOrDefaultAsync(x => x.GoBizOutletId == goBizOutletId && x.IsActive, ct)
            ?? throw new InvalidOperationException("Outlet GoBiz belum terhubung ke outlet Morrus POS.");

        // Opsi B: verifikasi signature per-Business (jika WebhookSecret dikonfigurasi).
        await VerifySignatureAsync(integration.OutletId, rawBody, headers, ct);

        var existing = await _dbContext.GoBizOrderInboxes.FirstOrDefaultAsync(x => x.GoBizOrderId == orderId, ct);
        if (existing is not null)
        {
            existing.ProcessedAtUtc ??= DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return;
        }

        _dbContext.GoBizOrderInboxes.Add(new GoBizOrderInbox
        {
            Id = Guid.NewGuid(),
            GoBizOrderId = orderId,
            OutletId = integration.OutletId,
            EventType = eventType,
            RawPayloadJson = rawBody,
            Status = "received",
            ReceivedAtUtc = DateTime.UtcNow
        });

        integration.LastWebhookAtUtc = DateTime.UtcNow;
        integration.UpdatedAt = DateTime.UtcNow;

        _dbContext.IntegrationLogs.Add(new IntegrationLog
        {
            Id = Guid.NewGuid(),
            ServiceName = "GoBiz Webhook - order",
            RequestPayload = rawBody.Length <= 8000 ? rawBody : rawBody[..8000],
            StatusCode = "200",
            IsSuccess = true,
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(ct);
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private async Task VerifySignatureAsync(Guid outletId, string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
    {
        ResolvedGoBizConfig cfg;
        try
        {
            cfg = await _configProvider.GetByOutletAsync(outletId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GoBiz webhook: gagal resolve config untuk outlet {OutletId}.", outletId);
            return;
        }

        if (string.IsNullOrWhiteSpace(cfg.WebhookSecret))
        {
            _logger.LogWarning("GoBiz webhook tanpa verifikasi signature untuk outlet {OutletId} (WebhookSecret belum dikonfigurasi, Source={Source}).", outletId, cfg.Source);
            return;
        }

        headers.TryGetValue("x-gobiz-signature", out var sig1);
        headers.TryGetValue("x-gojek-signature", out var sig2);
        headers.TryGetValue("signature", out var sig3);
        var provided = sig1 ?? sig2 ?? sig3;
        if (string.IsNullOrWhiteSpace(provided))
            throw new InvalidOperationException("Signature webhook GoBiz tidak ditemukan.");

        var expected = "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(cfg.WebhookSecret), Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
        var providedNorm = provided.Trim();
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(providedNorm.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? providedNorm : "sha256=" + providedNorm);
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            throw new InvalidOperationException("Signature webhook GoBiz tidak valid.");
    }
}
