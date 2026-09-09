using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizOAuthService : IGoBizOAuthService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoBizOAuthService> _logger;
    private readonly IGoBizConfigProvider _configProvider;
    private readonly IGoBizAuthService _authService;
    private readonly IGoBizOAuthStateStore _stateStore;

    public GoBizOAuthService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        HttpClient httpClient,
        ILogger<GoBizOAuthService> logger,
        IGoBizConfigProvider configProvider,
        IGoBizAuthService authService,
        IGoBizOAuthStateStore stateStore)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _httpClient = httpClient;
        _logger = logger;
        _configProvider = configProvider;
        _authService = authService;
        _stateStore = stateStore;
    }

    public Task<GoBizConnectUrlResponse> CreateConnectUrlAsync(Guid outletId, CancellationToken ct = default)
        => CreateConnectUrlInternalAsync(outletId, ct);

    private async Task<GoBizConnectUrlResponse> CreateConnectUrlInternalAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await GetAccessibleOutletAsync(outletId, ct);
        var cfg = await _configProvider.GetByOutletAsync(outlet.Id, ct);
        EnsureConfigured(cfg);

        await CleanupExpiredStatesAsync(outlet.Id, ct);

        return await _authService.CreateAuthorizationUrlAsync(outlet.Id, outlet.BusinessId, ct);
    }

    public async Task<GoBizCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GoBizOAuthException("GOBIZ_AUTHORIZATION_DENIED", "Authorization code GoBiz kosong.");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new GoBizOAuthException("GOBIZ_STATE_INVALID", "State OAuth GoBiz tidak valid.");
        }

        var oauthState = await _stateStore.ValidateAndConsumeAsync(state, ct);
        if (oauthState is null)
        {
            throw new GoBizOAuthException("GOBIZ_STATE_INVALID", "State OAuth GoBiz tidak valid atau sudah kedaluwarsa.");
        }

        var cfg = await _configProvider.GetByOutletAsync(oauthState.OutletId, ct);
        EnsureConfigured(cfg);

        var tokenResponse = await ExchangeAuthorizationCodeAsync(code, cfg, ct);

        var integration = await _dbContext.GoBizIntegrations.FirstOrDefaultAsync(x => x.OutletId == oauthState.OutletId, ct);
        if (integration is null)
        {
            integration = new GoBizIntegration
            {
                Id = Guid.NewGuid(),
                OutletId = oauthState.OutletId,
                BusinessId = oauthState.BusinessId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.GoBizIntegrations.Add(integration);
        }

        integration.AccessToken = tokenResponse.AccessToken;
        integration.RefreshToken = tokenResponse.RefreshToken;
        integration.TokenType = tokenResponse.TokenType;
        integration.Scope = tokenResponse.Scope;
        integration.ExpiresAtUtc = tokenResponse.ExpiresAtUtc;
        integration.ExternalMerchantId = tokenResponse.ExternalMerchantId;
        integration.RawMetadataJson = tokenResponse.RawPayloadJson;
        integration.IsActive = true;
        integration.ConnectedAtUtc = DateTime.UtcNow;
        integration.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("GoBiz connected for outlet {OutletId}.", oauthState.OutletId);
        return new GoBizCallbackResult(true, "Integrasi GoBiz berhasil dihubungkan.", oauthState.OutletId);
    }

    public async Task<GoBizConnectionStatusDto> GetStatusAsync(Guid outletId, CancellationToken ct = default)
    {
        await GetAccessibleOutletAsync(outletId, ct);

        var integration = await _dbContext.GoBizIntegrations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OutletId == outletId, ct);

        return new GoBizConnectionStatusDto(
            outletId,
            integration != null,
            integration?.IsActive ?? false,
            integration?.ConnectedAtUtc,
            integration?.ExpiresAtUtc,
            integration?.ExternalMerchantId,
            integration?.Scope);
    }

    public async Task DisconnectAsync(Guid outletId, CancellationToken ct = default)
    {
        await GetAccessibleOutletAsync(outletId, ct);

        var integration = await _dbContext.GoBizIntegrations.FirstOrDefaultAsync(x => x.OutletId == outletId, ct);
        if (integration is null)
        {
            return;
        }

        integration.IsActive = false;
        integration.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    public GoBizDebugConfigDto GetDebugConfig()
    {
        // Legacy sync API dipertahankan untuk kompatibilitas.
        // Untuk debug per-Business, gunakan IGoBizClientConfigService.GetDebugAsync.
        // Di sini kembalikan fallback aman tanpa throw agar /debug/config tidak 500 saat DB kosong.
        try
        {
            var cfg = _configProvider.GetByBusinessAsync(
                _currentUserService.BusinessId ?? Guid.Empty).GetAwaiter().GetResult();
            return new GoBizDebugConfigDto(
                cfg.Environment,
                cfg.RedirectUri,
                cfg.AuthorizationUrl,
                cfg.TokenUrl,
                !string.IsNullOrWhiteSpace(cfg.ClientId),
                !string.IsNullOrWhiteSpace(cfg.ClientSecret));
        }
        catch
        {
            return new GoBizDebugConfigDto("Sandbox", string.Empty, string.Empty, string.Empty, false, false);
        }
    }

    private async Task<GoBizTokenExchangeResult> ExchangeAuthorizationCodeAsync(string code, ResolvedGoBizConfig cfg, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, cfg.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = cfg.RedirectUri
            })
        };

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.ClientId}:{cfg.ClientSecret}"));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GoBiz token exchange failed. Status={StatusCode}.", (int)response.StatusCode);
            throw new GoBizOAuthException("GOBIZ_TOKEN_EXCHANGE_FAILED", "Gagal menukar authorization code GoBiz.");
        }

        var tokenPayload = JsonSerializer.Deserialize<GoBizTokenPayload>(payload, JsonOptions)
            ?? throw new GoBizOAuthException("GOBIZ_TOKEN_EXCHANGE_FAILED", "Response token GoBiz tidak valid.");

        return new GoBizTokenExchangeResult(
            tokenPayload.AccessToken ?? throw new GoBizOAuthException("GOBIZ_TOKEN_EXCHANGE_FAILED", "Access token GoBiz tidak ditemukan."),
            tokenPayload.RefreshToken,
            tokenPayload.TokenType,
            tokenPayload.Scope,
            tokenPayload.ExpiresIn.HasValue ? DateTime.UtcNow.AddSeconds(tokenPayload.ExpiresIn.Value) : null,
            ExtractMerchantId(tokenPayload, payload),
            payload);
    }

    private async Task<Outlet> GetAccessibleOutletAsync(Guid outletId, CancellationToken ct)
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

    private async Task CleanupExpiredStatesAsync(Guid? outletId, CancellationToken ct)
    {
        var query = _dbContext.GoBizOAuthStates
            .Where(x => !x.ConsumedAtUtc.HasValue)
            .Where(x => x.ExpiresAtUtc < DateTime.UtcNow);

        if (outletId.HasValue)
        {
            query = query.Where(x => x.OutletId == outletId.Value);
        }

        var expiredStates = await query.ToListAsync(ct);
        if (expiredStates.Count == 0)
        {
            return;
        }

        _dbContext.GoBizOAuthStates.RemoveRange(expiredStates);
        await _dbContext.SaveChangesAsync(ct);
    }

    private void EnsureConfigured(ResolvedGoBizConfig cfg)
    {
        if (string.IsNullOrWhiteSpace(cfg.ClientId) ||
            string.IsNullOrWhiteSpace(cfg.ClientSecret) ||
            string.IsNullOrWhiteSpace(cfg.AuthorizationUrl) ||
            string.IsNullOrWhiteSpace(cfg.TokenUrl) ||
            string.IsNullOrWhiteSpace(cfg.RedirectUri))
        {
            throw new GoBizOAuthException("GOBIZ_CONFIG_INVALID", "Konfigurasi GoBiz belum lengkap. Isi kredensial per-Business di menu Integrasi GoBiz.");
        }
    }

    private static string? ExtractMerchantId(GoBizTokenPayload tokenPayload, string rawPayloadJson)
    {
        using var document = JsonDocument.Parse(rawPayloadJson);
        var root = document.RootElement;

        if (TryGetString(root, "merchant_id", out var merchantId) ||
            TryGetString(root, "merchantId", out merchantId) ||
            TryGetString(root, "external_merchant_id", out merchantId) ||
            TryGetString(root, "externalMerchantId", out merchantId))
        {
            return merchantId;
        }

        if (TryGetString(root, "sub", out var subject))
        {
            return subject;
        }

        if (!string.IsNullOrWhiteSpace(tokenPayload.IdToken))
        {
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(tokenPayload.IdToken))
            {
                var jwt = handler.ReadJwtToken(tokenPayload.IdToken);
                var claim = jwt.Claims.FirstOrDefault(x =>
                    x.Type is "merchant_id" or "merchantId" or "external_merchant_id" or "externalMerchantId" or "sub");
                if (claim != null)
                {
                    return claim.Value;
                }
            }
        }

        return null;
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string? value)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private sealed record GoBizTokenExchangeResult(
        string AccessToken,
        string? RefreshToken,
        string? TokenType,
        string? Scope,
        DateTime? ExpiresAtUtc,
        string? ExternalMerchantId,
        string RawPayloadJson);

    private sealed class GoBizTokenPayload
    {
        public string? AccessToken { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? TokenType { get; set; }
        public string? Scope { get; set; }
        public int? ExpiresIn { get; set; }
    }
}

public sealed class GoBizOAuthException : Exception
{
    public GoBizOAuthException(string code, string message) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
