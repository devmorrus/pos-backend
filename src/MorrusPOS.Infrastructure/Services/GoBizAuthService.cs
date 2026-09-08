using MorrusPOS.Application.Features.Channels;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using MorrusPOS.Infrastructure.Options;

namespace MorrusPOS.Infrastructure.Services;

public interface IGoBizAuthService
{
    Task<GoBizConnectUrlResponse> CreateAuthorizationUrlAsync(Guid outletId, Guid? businessId, CancellationToken ct = default);
}

public sealed class GoBizAuthService : IGoBizAuthService
{
    private readonly GoBizOptions _options;
    private readonly IGoBizOAuthStateStore _stateStore;

    public GoBizAuthService(IOptions<GoBizOptions> options, IGoBizOAuthStateStore stateStore)
    {
        _options = options.Value;
        _stateStore = stateStore;
    }

    public async Task<GoBizConnectUrlResponse> CreateAuthorizationUrlAsync(Guid outletId, Guid? businessId, CancellationToken ct = default)
    {
        var state = await _stateStore.CreateAsync(outletId, businessId, TimeSpan.FromMinutes(10), ct);

        var url = QueryHelpers.AddQueryString(_options.AuthorizationUrl, new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = string.IsNullOrWhiteSpace(_options.Scope) ? "openid" : _options.Scope,
            ["state"] = state.State,
            ["redirect_uri"] = _options.RedirectUri,
            ["user_type"] = string.IsNullOrWhiteSpace(_options.UserType) ? "merchant" : _options.UserType,
            ["prompt"] = string.IsNullOrWhiteSpace(_options.Prompt) ? "login" : _options.Prompt
        });

        return new GoBizConnectUrlResponse(url, state.State, state.ExpiresAtUtc);
    }
}
