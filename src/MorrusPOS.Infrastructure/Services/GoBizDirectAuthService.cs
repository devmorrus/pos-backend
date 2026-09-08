using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Infrastructure.Options;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizDirectAuthService : IGoBizDirectAuthService
{
    private const string CacheKey = "gobiz:direct:access-token";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GoBizDirectAuthService> _logger;
    private readonly GoBizOptions _options;

    public GoBizDirectAuthService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<GoBizDirectAuthService> logger,
        IOptions<GoBizOptions> options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public async Task<GoBizDirectAccessToken> GetAccessTokenAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        EnsureConfigured();

        var cacheKey = $"{CacheKey}:{_options.Environment}";
        if (!forceRefresh && _cache.TryGetValue(cacheKey, out GoBizDirectAccessToken? cached) && cached != null)
        {
            return cached;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = _options.Scope
            })
        };

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GoBiz direct token request failed. Status={StatusCode}.", (int)response.StatusCode);
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
            tokenPayload.Scope ?? _options.Scope,
            expiresAtUtc);

        var cacheSeconds = Math.Max(30, expiresIn - Math.Max(0, _options.TokenRefreshSkewSeconds));
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(cacheSeconds));
        return result;
    }

    public async Task<GoBizDirectTokenStatusDto> TestTokenAsync(CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(forceRefresh: true, ct);
        return new GoBizDirectTokenStatusDto(true, token.TokenType, token.Scope, token.ExpiresAtUtc);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.TokenUrl) ||
            string.IsNullOrWhiteSpace(_options.Scope))
        {
            throw new InvalidOperationException("Konfigurasi GoBiz direct integration belum lengkap.");
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
