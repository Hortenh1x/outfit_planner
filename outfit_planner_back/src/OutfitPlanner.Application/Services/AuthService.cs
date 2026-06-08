using System.Net.Mail;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class AuthService
{
    private static readonly HashSet<string> AllowedExternalProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "google",
        "apple"
    };

    private readonly IUserAccountRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthTokenService _tokens;
    private readonly IClock _clock;
    private readonly TimeSpan _sessionLifetime = TimeSpan.FromDays(14);

    public AuthService(
        IUserAccountRepository users,
        IPasswordHasher passwordHasher,
        IAuthTokenService tokens,
        IClock clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
    }

    public AuthResult RegisterWithPassword(string email, string password, string repeatPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        var cleanPassword = RequirePassword(password);

        if (cleanPassword != repeatPassword)
        {
            throw new InvalidOperationException("Passwords do not match.");
        }

        if (_users.GetUserByNormalizedEmail(normalizedEmail) is not null)
        {
            throw new InvalidOperationException("An account with this email already exists.");
        }

        var now = _clock.UtcNow;
        var user = new UserAccount(
            CreateUserId(),
            normalizedEmail,
            normalizedEmail,
            DisplayNameFromEmail(normalizedEmail),
            _passwordHasher.HashPassword(cleanPassword),
            now,
            now,
            now);

        _users.AddUser(user);
        return CreateAuthResult(user, now);
    }

    public AuthResult SignInWithPassword(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = _users.GetUserByNormalizedEmail(normalizedEmail);
        if (user?.PasswordHash is null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            throw new InvalidOperationException("Email or password is invalid.");
        }

        var now = _clock.UtcNow;
        var updated = user with { LastLoginAt = now, UpdatedAt = now };
        _users.UpdateUser(updated);
        return CreateAuthResult(updated, now);
    }

    public AuthResult SignInWithExternalAccount(ExternalSignInCommand command)
    {
        var provider = NormalizeProvider(command.Provider);
        var providerSubject = RequireText(command.ProviderSubject, "External provider subject");
        var normalizedEmail = string.IsNullOrWhiteSpace(command.Email) ? null : NormalizeEmail(command.Email);
        var now = _clock.UtcNow;

        var existingLogin = _users.GetExternalLogin(provider, providerSubject);
        if (existingLogin is not null)
        {
            var user = _users.GetUserById(existingLogin.UserId)
                ?? throw new InvalidOperationException("Linked account no longer exists.");
            _users.UpdateExternalLogin(existingLogin with { Email = normalizedEmail, LastLoginAt = now });
            var updated = user with { LastLoginAt = now, UpdatedAt = now };
            _users.UpdateUser(updated);
            return CreateAuthResult(updated, now);
        }

        var linkedUser = normalizedEmail is not null && command.EmailVerified
            ? _users.GetUserByNormalizedEmail(normalizedEmail)
            : null;

        var userForLogin = linkedUser ?? CreateExternalUser(normalizedEmail, command.DisplayName, now);
        if (linkedUser is null)
        {
            _users.AddUser(userForLogin);
        }

        _users.AddExternalLogin(new ExternalAuthLogin(provider, providerSubject, userForLogin.Id, normalizedEmail, now, now));
        var updatedUser = userForLogin with { LastLoginAt = now, UpdatedAt = now };
        _users.UpdateUser(updatedUser);
        return CreateAuthResult(updatedUser, now);
    }

    public AuthenticatedSession? AuthenticateSession(string? sessionToken, string? csrfToken, bool requireCsrf)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var session = _users.GetActiveAuthSessionByTokenHash(_tokens.HashToken(sessionToken), _clock.UtcNow);
        if (session is null)
        {
            return null;
        }

        if (requireCsrf)
        {
            if (string.IsNullOrWhiteSpace(csrfToken) || _tokens.HashToken(csrfToken) != session.CsrfTokenHash)
            {
                return null;
            }
        }

        var user = _users.GetUserById(session.UserId);
        return user is null ? null : new AuthenticatedSession(ToPublicUser(user), session.ExpiresAt);
    }

    public void RevokeSession(string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return;
        }

        _users.RevokeAuthSessionByTokenHash(_tokens.HashToken(sessionToken), _clock.UtcNow);
    }

    private AuthResult CreateAuthResult(UserAccount user, DateTimeOffset now)
    {
        var sessionToken = _tokens.CreateToken();
        var csrfToken = _tokens.CreateToken();
        var expiresAt = now.Add(_sessionLifetime);

        _users.AddAuthSession(new AuthSession(
            Guid.NewGuid(),
            user.Id,
            _tokens.HashToken(sessionToken),
            _tokens.HashToken(csrfToken),
            expiresAt,
            now,
            null));

        return new AuthResult(ToPublicUser(user), sessionToken, csrfToken, expiresAt);
    }

    private static UserAccount CreateExternalUser(string? normalizedEmail, string? displayName, DateTimeOffset now)
    {
        var cleanDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? normalizedEmail is null ? "Outfit Planner user" : DisplayNameFromEmail(normalizedEmail)
            : displayName.Trim();

        return new UserAccount(
            CreateUserId(),
            normalizedEmail,
            normalizedEmail,
            cleanDisplayName,
            null,
            now,
            now,
            now);
    }

    private static PublicUser ToPublicUser(UserAccount user)
    {
        return new PublicUser(user.Id, user.Email, user.DisplayName);
    }

    private static string NormalizeProvider(string provider)
    {
        var normalized = RequireText(provider, "External provider").ToLowerInvariant();
        if (!AllowedExternalProviders.Contains(normalized))
        {
            throw new InvalidOperationException("Unsupported external auth provider.");
        }

        return normalized;
    }

    private static string NormalizeEmail(string email)
    {
        try
        {
            var address = new MailAddress(RequireText(email, "Email"));
            return address.Address.Trim().ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Email must be a valid address.");
        }
    }

    private static string RequirePassword(string password)
    {
        var clean = RequireText(password, "Password");
        if (clean.Length < 8)
        {
            throw new InvalidOperationException("Password must be at least 8 characters.");
        }

        if (!clean.Any(char.IsLetter))
        {
            throw new InvalidOperationException("Password must contain at least one letter.");
        }

        if (!clean.Any(char.IsDigit))
        {
            throw new InvalidOperationException("Password must contain at least one digit.");
        }

        return clean;
    }

    private static string RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        return value.Trim();
    }

    private static string DisplayNameFromEmail(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    private static string CreateUserId()
    {
        return $"usr_{Guid.NewGuid():N}";
    }
}

public sealed record ExternalSignInCommand(
    string Provider,
    string ProviderSubject,
    string? Email,
    bool EmailVerified,
    string? DisplayName);

public sealed record PublicUser(string Id, string? Email, string DisplayName);

public sealed record AuthResult(PublicUser User, string SessionToken, string CsrfToken, DateTimeOffset ExpiresAt);

public sealed record AuthenticatedSession(PublicUser User, DateTimeOffset ExpiresAt);
