using System.Security.Cryptography;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Security;

public sealed class SecureShareTokenGenerator : IShareTokenGenerator
{
    public string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}
