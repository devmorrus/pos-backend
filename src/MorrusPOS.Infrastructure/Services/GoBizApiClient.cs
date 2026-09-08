using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Infrastructure.Options;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizApiClient : IGoBizApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IGoBizDirectAuthService _authService;
    private readonly ILogger<GoBizApiClient> _logger;
    private readonly GoBizOptions _options;

    public GoBizApiClient(
        HttpClient httpClient,
        IGoBizDirectAuthService authService,
        ILogger<GoBizApiClient> logger,
        IOptions<GoBizOptions> options)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds));
    }

    public Task<JsonDocument> GetCatalogAsync(string goBizOutletId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, $"integrations/gofood/outlets/{Uri.EscapeDataString(goBizOutletId)}/v2/catalog", null, ct);

    public Task<JsonDocument> UpdateCatalogAsync(string goBizOutletId, JsonElement payload, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, $"integrations/gofood/outlets/{Uri.EscapeDataString(goBizOutletId)}/v1/catalog", payload.GetRawText(), ct);

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, string? jsonBody, CancellationToken ct)
    {
        var token = await _authService.GetAccessTokenAsync(ct: ct);
        using var response = await SendOnceAsync(method, path, jsonBody, token.AccessToken, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            token = await _authService.GetAccessTokenAsync(forceRefresh: true, ct);
            using var retryResponse = await SendOnceAsync(method, path, jsonBody, token.AccessToken, ct);
            return await ReadResponseAsync(retryResponse, path, ct);
        }

        return await ReadResponseAsync(response, path, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, string? jsonBody, string accessToken, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (jsonBody != null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<JsonDocument> ReadResponseAsync(HttpResponseMessage response, string path, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GoBiz API request failed. Path={Path}. Status={StatusCode}. Body={Body}", path, (int)response.StatusCode, body);
            throw new InvalidOperationException($"Request GoBiz gagal dengan status {(int)response.StatusCode}. Response={body}");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            body = "{}";
        }

        return JsonDocument.Parse(body);
    }
}
