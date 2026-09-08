namespace MorrusPOS.Infrastructure.Options;

public class GoBizOptions
{
    public const string SectionName = "GoBiz";

    public string Environment { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string AuthorizationUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid";
    public string UserType { get; set; } = "merchant";
    public string Prompt { get; set; } = "login";
    public string DirectOutletId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int TokenRefreshSkewSeconds { get; set; } = 300;
    public int RequestTimeoutSeconds { get; set; } = 30;
}
