using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record AdminUsersPage(IReadOnlyList<AdminUserRecord> Items, int TotalCount, int Offset, int Limit);

public sealed class AdminService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    private readonly IAdminUserRepository _adminUsers;
    private readonly IUserAccountRepository _users;
    private readonly RolePinningPolicy _rolePinning;
    private readonly IClock _clock;
    private readonly ICreditLedgerRepository? _creditLedger;

    public AdminService(
        IAdminUserRepository adminUsers,
        IUserAccountRepository users,
        RolePinningPolicy rolePinning,
        IClock clock,
        ICreditLedgerRepository? creditLedger = null)
    {
        _adminUsers = adminUsers;
        _users = users;
        _rolePinning = rolePinning;
        _clock = clock;
        _creditLedger = creditLedger;
    }

    public AdminUsersPage ListUsers(string? search, UserRole? role, int offset, int limit)
    {
        var query = new AdminUserQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            role,
            Math.Max(offset, 0),
            Math.Clamp(limit <= 0 ? DefaultPageSize : limit, 1, MaxPageSize));

        return new AdminUsersPage(
            _adminUsers.ListUsers(query, _clock.UtcNow),
            _adminUsers.CountUsers(query),
            query.Offset,
            query.Limit);
    }

    public AdminUserRecord? GetUser(string userId)
    {
        return _adminUsers.GetUserRecord(userId, _clock.UtcNow);
    }

    public AdminUserStats Stats()
    {
        return _adminUsers.GetStats();
    }

    public AdminUserRecord? ChangeRole(string actingUserId, string targetUserId, UserRole role)
    {
        var target = _users.GetUserById(targetUserId);
        if (target is null)
        {
            return null;
        }

        if (string.Equals(actingUserId, targetUserId, StringComparison.Ordinal))
        {
            throw new ValidationException("You cannot change your own role.");
        }

        if (_rolePinning.IsPinned(target.NormalizedEmail))
        {
            throw new ValidationException("This account's role is pinned and cannot be changed.");
        }

        _users.UpdateUser(target with { Role = role, UpdatedAt = _clock.UtcNow });
        return _adminUsers.GetUserRecord(targetUserId, _clock.UtcNow);
    }

    // Deletion guard for the admin panel: self-deletion stays on DELETE /api/account, and the
    // pinned (always-admin / always-premium) accounts cannot be removed by another admin.
    public UserAccount? RequireDeletableUser(string actingUserId, string targetUserId)
    {
        var target = _users.GetUserById(targetUserId);
        if (target is null)
        {
            return null;
        }

        if (string.Equals(actingUserId, targetUserId, StringComparison.Ordinal))
        {
            throw new ValidationException("You cannot delete your own account from the admin panel.");
        }

        if (_rolePinning.IsPinned(target.NormalizedEmail))
        {
            throw new ValidationException("This account is pinned and cannot be deleted.");
        }

        return target;
    }

    // Raw ledger balance for the admin panel — intentionally read-only (no lazy grants),
    // so listing users never writes; null for unlimited (Admin) accounts.
    public int? RawCreditBalance(UserAccount user)
    {
        if (_creditLedger is null || _rolePinning.EffectiveRole(user) == UserRole.Admin)
        {
            return null;
        }

        return _creditLedger.GetCreditBalance(user.Id, _clock.UtcNow);
    }

    public UserRole EffectiveRole(UserAccount user)
    {
        return _rolePinning.EffectiveRole(user);
    }

    public bool IsPinned(UserAccount user)
    {
        return _rolePinning.IsPinned(user.NormalizedEmail);
    }
}
