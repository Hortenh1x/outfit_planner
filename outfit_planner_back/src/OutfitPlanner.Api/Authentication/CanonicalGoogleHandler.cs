using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

namespace OutfitPlanner.Api.Authentication;

public sealed class CanonicalGoogleHandler : GoogleHandler
{
    private readonly IConfiguration _configuration;

    public CanonicalGoogleHandler(
        IOptionsMonitor<GoogleOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        var publicOrigin = NormalizePublicOrigin(_configuration["Authentication:PublicOrigin"]);
        if (publicOrigin is null)
        {
            return base.ExchangeCodeAsync(context);
        }

        var canonicalContext = new OAuthCodeExchangeContext(
            context.Properties,
            context.Code,
            $"{publicOrigin}{Options.CallbackPath}");

        return base.ExchangeCodeAsync(canonicalContext);
    }

    private static string? NormalizePublicOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return null;
        }

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("Authentication:PublicOrigin must be an absolute HTTP or HTTPS origin.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
