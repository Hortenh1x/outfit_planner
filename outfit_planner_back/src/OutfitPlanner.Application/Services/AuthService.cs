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
    private readonly RolePinningPolicy _rolePinning;
    private readonly TimeSpan _sessionLifetime = TimeSpan.FromDays(14);
    private readonly TimeSpan _emailVerificationLifetime = TimeSpan.FromHours(24);
    private readonly TimeSpan _passwordResetLifetime = TimeSpan.FromHours(1);

    public AuthService(
        IUserAccountRepository users,
        IPasswordHasher passwordHasher,
        IAuthTokenService tokens,
        IClock clock,
        RolePinningPolicy rolePinning)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _clock = clock;
        _rolePinning = rolePinning;
    }

    public AuthResult RegisterWithPassword(string email, string password, string repeatPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        var cleanPassword = RequirePassword(password);

        if (cleanPassword != repeatPassword)
        {
            throw new ValidationException("Passwords do not match.");
        }

        if (_users.GetUserByNormalizedEmail(normalizedEmail) is not null)
        {
            throw new ValidationException("An account with this email already exists.");
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
            now)
        {
            Role = _rolePinning.PinnedRole(normalizedEmail) ?? UserRole.Free
        };

        _users.AddUser(user);
        return CreateAuthResult(user, now);
    }

    public AuthResult SignInWithPassword(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = _users.GetUserByNormalizedEmail(normalizedEmail);
        if (user?.PasswordHash is null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
        {
            throw new ValidationException("Email or password is invalid.");
        }

        var now = _clock.UtcNow;
        // Fold the effective role into the login write so pinned accounts converge in every
        // store, including the file-backed one that never runs SQL migrations.
        var updated = user with { LastLoginAt = now, UpdatedAt = now, Role = _rolePinning.EffectiveRole(user) };
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
                ?? throw new ValidationException("Linked account no longer exists.");
            _users.UpdateExternalLogin(existingLogin with { Email = normalizedEmail, LastLoginAt = now });
            var updated = user with { LastLoginAt = now, UpdatedAt = now, Role = _rolePinning.EffectiveRole(user) };
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
        var updatedUser = userForLogin with { LastLoginAt = now, UpdatedAt = now, Role = _rolePinning.EffectiveRole(userForLogin) };
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
            if (string.IsNullOrWhiteSpace(csrfToken) || !FixedTimeEquals(_tokens.HashToken(csrfToken), session.CsrfTokenHash))
            {
                return null;
            }
        }

        var user = _users.GetUserById(session.UserId);
        return user is null ? null : new AuthenticatedSession(ToPublicUser(user), session.ExpiresAt);
    }

    // Constant-time comparison so CSRF token-hash validation does not leak match progress via timing.
    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    public void RevokeSession(string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return;
        }

        _users.RevokeAuthSessionByTokenHash(_tokens.HashToken(sessionToken), _clock.UtcNow);
    }

    public IReadOnlyList<AuthSessionInfo> ListSessions(string? sessionToken)
    {
        var session = RequireActiveSession(sessionToken);
        return _users.ListAuthSessionsByUser(session.UserId, _clock.UtcNow)
            .Select(item => new AuthSessionInfo(item.Id, item.CreatedAt, item.ExpiresAt, item.RevokedAt))
            .ToList();
    }

    public void RevokeAllSessions(string? sessionToken)
    {
        var session = RequireActiveSession(sessionToken);
        _users.RevokeAuthSessionsByUser(session.UserId, _clock.UtcNow);
    }

    public int CleanupExpiredSessions()
    {
        return _users.DeleteExpiredAuthSessions(_clock.UtcNow);
    }

    public PublicUser UpdateProfile(string userId, string username, UserGender? gender)
    {
        var cleanUserId = RequireText(userId, "User id");
        var cleanUsername = NormalizeUsername(username);
        var user = _users.GetUserById(cleanUserId)
            ?? throw new InvalidOperationException("Account was not found.");
        var now = _clock.UtcNow;
        var updated = user with
        {
            DisplayName = cleanUsername,
            Gender = gender,
            UpdatedAt = now
        };

        _users.UpdateUser(updated);
        return ToPublicUser(updated);
    }

    public PublicUser UpdateAvatar(string userId, string avatarUrl, string? avatarObjectKey)
    {
        var cleanUserId = RequireText(userId, "User id");
        var cleanAvatarUrl = RequireText(avatarUrl, "Avatar URL");
        var user = _users.GetUserById(cleanUserId)
            ?? throw new InvalidOperationException("Account was not found.");
        var now = _clock.UtcNow;
        var updated = user with
        {
            AvatarUrl = cleanAvatarUrl,
            AvatarObjectKey = string.IsNullOrWhiteSpace(avatarObjectKey) ? null : avatarObjectKey.Trim(),
            UpdatedAt = now
        };

        _users.UpdateUser(updated);
        return ToPublicUser(updated);
    }

    public string CreateEmailVerificationToken(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = _users.GetUserByNormalizedEmail(normalizedEmail)
            ?? throw new InvalidOperationException("Account was not found.");
        var token = _tokens.CreateToken();
        var now = _clock.UtcNow;
        _users.AddEmailVerificationToken(new AuthEmailVerificationToken(
            _tokens.HashToken(token),
            user.Id,
            now.Add(_emailVerificationLifetime),
            now,
            null));
        return token;
    }

    public bool ConfirmEmailVerification(string token)
    {
        var now = _clock.UtcNow;
        var tokenHash = _tokens.HashToken(RequireText(token, "Verification token"));
        var verification = _users.GetActiveEmailVerificationToken(tokenHash, now);
        if (verification is null)
        {
            return false;
        }

        var user = _users.GetUserById(verification.UserId);
        if (user is null)
        {
            return false;
        }

        _users.UpdateUser(user with { EmailVerifiedAt = now, UpdatedAt = now });
        _users.MarkEmailVerificationTokenUsed(tokenHash, now);
        return true;
    }

    public string CreatePasswordResetToken(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = _users.GetUserByNormalizedEmail(normalizedEmail)
            ?? throw new InvalidOperationException("Account was not found.");
        var token = _tokens.CreateToken();
        var now = _clock.UtcNow;
        _users.AddPasswordResetToken(new AuthPasswordResetToken(
            _tokens.HashToken(token),
            user.Id,
            now.Add(_passwordResetLifetime),
            now,
            null));
        return token;
    }

    public bool ResetPassword(string token, string password, string repeatPassword)
    {
        var cleanPassword = RequirePassword(password);
        if (cleanPassword != repeatPassword)
        {
            throw new ValidationException("Passwords do not match.");
        }

        var now = _clock.UtcNow;
        var tokenHash = _tokens.HashToken(RequireText(token, "Password reset token"));
        var reset = _users.GetActivePasswordResetToken(tokenHash, now);
        if (reset is null)
        {
            return false;
        }

        var user = _users.GetUserById(reset.UserId);
        if (user is null)
        {
            return false;
        }

        _users.UpdateUser(user with
        {
            PasswordHash = _passwordHasher.HashPassword(cleanPassword),
            UpdatedAt = now
        });
        _users.MarkPasswordResetTokenUsed(tokenHash, now);
        _users.RevokeAuthSessionsByUser(user.Id, now);
        return true;
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

    private AuthSession RequireActiveSession(string? sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new InvalidOperationException("Authentication is required.");
        }

        return _users.GetActiveAuthSessionByTokenHash(_tokens.HashToken(sessionToken), _clock.UtcNow)
            ?? throw new InvalidOperationException("Authentication is required.");
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

    private PublicUser ToPublicUser(UserAccount user)
    {
        return new PublicUser(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.Gender, _rolePinning.EffectiveRole(user));
    }

    private static string NormalizeProvider(string provider)
    {
        var normalized = RequireText(provider, "External provider").ToLowerInvariant();
        if (!AllowedExternalProviders.Contains(normalized))
        {
            throw new ValidationException("Unsupported external auth provider.");
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
            throw new ValidationException("Email must be a valid address.");
        }
    }

    private static string RequirePassword(string password)
    {
        var clean = RequireText(password, "Password");
        if (clean.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters.");
        }

        if (!clean.Any(char.IsLetter))
        {
            throw new ValidationException("Password must contain at least one letter.");
        }

        if (!clean.Any(char.IsDigit))
        {
            throw new ValidationException("Password must contain at least one digit.");
        }

        return clean;
    }

    private static string RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{label} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeUsername(string username)
    {
        var clean = RequireText(username, "Username");
        if (clean.Length > 80)
        {
            throw new ValidationException("Username must be 80 characters or shorter.");
        }

        return clean;
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

public sealed record PublicUser(string Id, string? Email, string Username, string? AvatarUrl, UserGender? Gender, UserRole Role = UserRole.Free)
{
    public string DisplayName => Username;
}

public sealed record AuthResult(PublicUser User, string SessionToken, string CsrfToken, DateTimeOffset ExpiresAt);

public sealed record AuthenticatedSession(PublicUser User, DateTimeOffset ExpiresAt);

public sealed record AuthSessionInfo(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);
