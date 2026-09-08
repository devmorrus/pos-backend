using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizOAuthStateStore : IGoBizOAuthStateStore
{
    private const string CacheKeyPrefix = "gobiz-oauth-state:";
    private readonly IMemoryCache _cache;

    public GoBizOAuthStateStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<GoBizOAuthStateContext> CreateAsync(Guid outletId, Guid? businessId, TimeSpan ttl, CancellationToken ct = default)
    {
        var state = GenerateState();
        var context = new GoBizOAuthStateContext(state, outletId, businessId, DateTime.UtcNow.Add(ttl));

        _cache.Set(CacheKeyPrefix + state, context, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        });

        return Task.FromResult(context);
    }

    public Task<GoBizOAuthStateContext?> ValidateAndConsumeAsync(string state, CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(CacheKeyPrefix + state, out GoBizOAuthStateContext? context))
        {
            return Task.FromResult<GoBizOAuthStateContext?>(null);
        }

        _cache.Remove(CacheKeyPrefix + state);
        if (context is null || context.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Task.FromResult<GoBizOAuthStateContext?>(null);
        }

        return Task.FromResult<GoBizOAuthStateContext?>(context);
    }

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
