using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record RolePinningOptions(
    IReadOnlyList<string> AdminEmails,
    IReadOnlyList<string> PremiumEmails)
{
    public static RolePinningOptions Empty { get; } = new(Array.Empty<string>(), Array.Empty<string>());
}

// Pins account roles by normalized email: a pinned account keeps its role no matter what is
// stored or what the admin panel tries to set, in every store. Admin pins win when an email
// appears in both lists.
public sealed class RolePinningPolicy
{
    private readonly Dictionary<string, UserRole> _pins;

    public RolePinningPolicy(RolePinningOptions options)
    {
        _pins = new Dictionary<string, UserRole>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in options.PremiumEmails)
        {
            if (NormalizePin(email) is { } normalized)
            {
                _pins[normalized] = UserRole.Premium;
            }
        }

        foreach (var email in options.AdminEmails)
        {
            if (NormalizePin(email) is { } normalized)
            {
                _pins[normalized] = UserRole.Admin;
            }
        }
    }

    public UserRole? PinnedRole(string? normalizedEmail)
    {
        return normalizedEmail is not null && _pins.TryGetValue(normalizedEmail, out var role)
            ? role
            : null;
    }

    public bool IsPinned(string? normalizedEmail)
    {
        return normalizedEmail is not null && _pins.ContainsKey(normalizedEmail);
    }

    public UserRole EffectiveRole(UserAccount user)
    {
        return PinnedRole(user.NormalizedEmail) ?? user.Role;
    }

    private static string? NormalizePin(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
