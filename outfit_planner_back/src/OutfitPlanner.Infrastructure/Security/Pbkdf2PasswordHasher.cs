using System.Security.Cryptography;
using System.Text;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int Iterations = 210_000;
    private const string Algorithm = "PBKDF2-SHA256";

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyBytes);

        return string.Join('$', Algorithm, Iterations, Base64UrlEncode(salt), Base64UrlEncode(key));
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Base64UrlDecode(parts[2]);
            var expected = Base64UrlDecode(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

public sealed class SecureAuthTokenService : IAuthTokenService
{
    public string CreateToken()
    {
        return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    public string HashToken(string token)
    {
        return Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
