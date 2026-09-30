using MorrusPOS.Application.Features.Channels;
using Microsoft.AspNetCore.WebUtilities;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.Infrastructure.Services;

public interface IGoBizAuthService
{
    Task<GoBizConnectUrlResponse> CreateAuthorizationUrlAsync(Guid outletId, Guid? businessId, CancellationToken ct = default);
}

public sealed class GoBizAuthService : IGoBizAuthService
{
    private readonly IGoBizConfigProvider _configProvider;
    private readonly IGoBizOAuthStateStore _stateStore;

    public GoBizAuthService(IGoBizConfigProvider configProvider, IGoBizOAuthStateStore stateStore)
    {
        _configProvider = configProvider;
        _stateStore = stateStore;
    }

    public async Task<GoBizConnectUrlResponse> CreateAuthorizationUrlAsync(Guid outletId, Guid? businessId, CancellationToken ct = default)
    {
        var state = await _stateStore.CreateAsync(outletId, businessId, TimeSpan.FromMinutes(10), ct);
        var cfg = await _configProvider.GetByOutletAsync(outletId, ct);

        var url = QueryHelpers.AddQueryString(cfg.AuthorizationUrl, new Dictionary<string, string?>
        {
            ["client_id"] = cfg.ClientId,
            ["response_type"] = "code",
            ["scope"] = string.IsNullOrWhiteSpace(cfg.Scope) ? "openid" : cfg.Scope,
            ["state"] = state.State,
            ["redirect_uri"] = cfg.RedirectUri,
            ["user_type"] = string.IsNullOrWhiteSpace(cfg.UserType) ? "merchant" : cfg.UserType,
            ["prompt"] = string.IsNullOrWhiteSpace(cfg.Prompt) ? "login" : cfg.Prompt
        });

        return new GoBizConnectUrlResponse(url, state.State, state.ExpiresAtUtc);
    }
}
