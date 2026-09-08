using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MorrusPOS.Infrastructure.Options;

public sealed class GoBizOptionsValidator : IValidateOptions<GoBizOptions>
{
    private readonly IHostEnvironment _hostEnvironment;

    public GoBizOptionsValidator(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    public ValidateOptionsResult Validate(string? name, GoBizOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId)) errors.Add("GoBiz:ClientId is required.");
        if (string.IsNullOrWhiteSpace(options.ClientSecret)) errors.Add("GoBiz:ClientSecret is required.");
        if (string.IsNullOrWhiteSpace(options.RedirectUri)) errors.Add("GoBiz:RedirectUri is required.");
        if (string.IsNullOrWhiteSpace(options.AuthorizationUrl)) errors.Add("GoBiz:AuthorizationUrl is required.");
        if (string.IsNullOrWhiteSpace(options.TokenUrl)) errors.Add("GoBiz:TokenUrl is required.");
        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl)) errors.Add("GoBiz:ApiBaseUrl is required.");

        if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl) &&
            !Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out _))
        {
            errors.Add("GoBiz:ApiBaseUrl must be an absolute URI.");
        }

        if (options.TokenRefreshSkewSeconds < 0)
        {
            errors.Add("GoBiz:TokenRefreshSkewSeconds must be zero or greater.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            errors.Add("GoBiz:RequestTimeoutSeconds must be greater than zero.");
        }

        ValidateRedirectUri(options.RedirectUri, errors);

        return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
    }

    private void ValidateRedirectUri(string redirectUri, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return;
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            errors.Add("GoBiz:RedirectUri must be an absolute URI.");
            return;
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            errors.Add("GoBiz:RedirectUri must not contain query parameters.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            if (!_hostEnvironment.IsDevelopment() ||
                !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("GoBiz:RedirectUri must use HTTPS.");
            }
        }
    }
}
