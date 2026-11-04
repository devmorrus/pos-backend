using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class IdempotencyService : IIdempotencyService
{
    private static readonly JsonSerializerOptions HashJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<IdempotencyService>? _logger;

    public IdempotencyService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<IdempotencyService>? logger = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public string ComputeRequestHash<T>(T request)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, HashJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public async Task<IdempotencyClaim> ClaimAsync(
        string operation,
        Guid key,
        string requestHash,
        Guid userId,
        Guid? outletId,
        CancellationToken ct = default)
    {
        if (key == Guid.Empty)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyRequired,
                "Idempotency key wajib tersedia.");
        if (!IdempotencyOperation.All.Contains(operation) || requestHash.Length != 64)
            throw new IdempotencyException(IdempotencyErrorCodes.KeyInvalid,
                "Idempotency operation atau request hash tidak valid.");
        var businessId = _currentUser.BusinessId
            ?? throw new UnauthorizedAccessException("Business context wajib tersedia untuk idempotency.");

        if (_dbContext.Database.IsRelational())
        {
            if (_dbContext.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Idempotency claim wajib dijalankan di dalam database transaction.");
            var lockKey = $"{businessId:N}:{operation}:{key:N}";
            var acquired = await _dbContext.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({lockKey},0)) AS \"Value\"")
                .SingleAsync(ct);
            if (!acquired)
            {
                _logger?.LogWarning("IdempotencyInProgress BusinessId={BusinessId} Operation={Operation} Key={Key}", businessId, operation, key);
                throw new IdempotencyException(IdempotencyErrorCodes.InProgress,
                    "Request dengan idempotency key ini masih diproses.");
            }
        }

        var existing = await _dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Operation == operation && x.IdempotencyKey == key, ct);
        if (existing is null)
        {
            _logger?.LogInformation("IdempotencyClaimed BusinessId={BusinessId} Operation={Operation} Key={Key}", businessId, operation, key);
            return new IdempotencyClaim(operation, key, requestHash, userId, outletId,
                false, null, null);
        }
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            _logger?.LogWarning("IdempotencyConflict BusinessId={BusinessId} Operation={Operation} Key={Key}", businessId, operation, key);
            throw new IdempotencyException(IdempotencyErrorCodes.KeyReused,
                "Idempotency key sudah digunakan untuk payload yang berbeda.");
        }
        _logger?.LogInformation("IdempotencyReplayed BusinessId={BusinessId} Operation={Operation} Key={Key} ResourceId={ResourceId}",
            businessId, operation, key, existing.ResourceId);
        return new IdempotencyClaim(operation, key, requestHash, userId, outletId,
            true, existing.ResourceType, existing.ResourceId);
    }

    public void Complete(IdempotencyClaim claim, string resourceType, Guid resourceId)
    {
        if (claim.Replayed) return;
        _dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = claim.Key,
            Operation = claim.Operation,
            RequestHash = claim.RequestHash,
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = claim.UserId,
            OutletId = claim.OutletId,
            CreatedAt = DateTime.UtcNow
        });
    }
}
