using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MorrusPOS.Application.Features.Channels;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizDirectAuthService : IGoBizDirectAuthService
{
    private const string CacheKeyPrefix = "gobiz:direct:access-token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GoBizDirectAuthService> _logger;
    private readonly IGoBizConfigProvider _configProvider;

    public GoBizDirectAuthService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<GoBizDirectAuthService> logger,
        IGoBizConfigProvider configProvider)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _configProvider = configProvider;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<GoBizDirectAccessToken> GetAccessTokenAsync(Guid outletId, bool forceRefresh = false, CancellationToken ct = default)
    {
        var cfg = await _configProvider.GetByOutletAsync(outletId, ct);
        EnsureConfigured(cfg);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, cfg.RequestTimeoutSeconds));

        var businessKey = cfg.BusinessId?.ToString() ?? outletId.ToString();
        var cacheKey = $"{CacheKeyPrefix}:{businessKey}:{cfg.Environment}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out GoBizDirectAccessToken? cached) && cached != null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, cfg.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = cfg.Scope
            })
        };

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.ClientId}:{cfg.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GoBiz direct token request failed. Outlet={OutletId}. Status={StatusCode}.", outletId, (int)response.StatusCode);
            throw new InvalidOperationException($"Gagal mengambil access token GoBiz direct integration. Status={(int)response.StatusCode}. Response={payload}");
        }

        var tokenPayload = JsonSerializer.Deserialize<GoBizDirectTokenPayload>(payload, JsonOptions)
            ?? throw new InvalidOperationException("Response access token GoBiz tidak valid.");

        if (string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            throw new InvalidOperationException($"Access token GoBiz tidak ditemukan pada response. Response={payload}");
        }

        var expiresIn = tokenPayload.ExpiresIn <= 0 ? 3599 : tokenPayload.ExpiresIn;
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);
        var result = new GoBizDirectAccessToken(
            tokenPayload.AccessToken,
            string.IsNullOrWhiteSpace(tokenPayload.TokenType) ? "bearer" : tokenPayload.TokenType,
            tokenPayload.Scope ?? cfg.Scope,
            expiresAtUtc);

        var cacheSeconds = Math.Max(30, expiresIn - Math.Max(0, cfg.TokenRefreshSkewSeconds));
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(cacheSeconds));
        return result;
    }

    public async Task<GoBizDirectTokenStatusDto> TestTokenAsync(Guid outletId, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(outletId, forceRefresh: true, ct);
        return new GoBizDirectTokenStatusDto(true, token.TokenType, token.Scope, token.ExpiresAtUtc);
    }

    private static void EnsureConfigured(ResolvedGoBizConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.ClientId) ||
            string.IsNullOrWhiteSpace(cfg.ClientSecret) ||
            string.IsNullOrWhiteSpace(cfg.TokenUrl) ||
            string.IsNullOrWhiteSpace(cfg.Scope))
        {
            throw new InvalidOperationException("Konfigurasi GoBiz direct integration belum lengkap. Isi kredensial per-Business di menu Integrasi GoBiz.");
        }
    }

    private sealed class GoBizDirectTokenPayload
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
