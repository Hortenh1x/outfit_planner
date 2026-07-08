using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record AccountEntitlements(
    UserRole Role,
    PlanLimits Limits,
    int GarmentCount,
    int OutfitCount,
    int BodyReferencePhotoCount,
    CreditBalanceInfo Credits);

// Resolves what an account may do under its effective role: plan limits, current usage,
// and the credit balance. Creation caps throw the same ValidationException surface the
// rest of the app maps to 400, with upgrade-friendly messages.
public sealed class EntitlementService
{
    private readonly IUserAccountRepository _users;
    private readonly IGarmentRepository _garments;
    private readonly IOutfitRepository _outfits;
    private readonly IBodyReferencePhotoRepository _bodyPhotos;
    private readonly PlanCatalog _plans;
    private readonly RolePinningPolicy _rolePinning;
    private readonly CreditLedgerService _credits;

    public EntitlementService(
        IUserAccountRepository users,
        IGarmentRepository garments,
        IOutfitRepository outfits,
        IBodyReferencePhotoRepository bodyPhotos,
        PlanCatalog plans,
        RolePinningPolicy rolePinning,
        CreditLedgerService credits)
    {
        _users = users;
        _garments = garments;
        _outfits = outfits;
        _bodyPhotos = bodyPhotos;
        _plans = plans;
        _rolePinning = rolePinning;
        _credits = credits;
    }

    public AccountEntitlements Get(string userId)
    {
        var user = RequireUser(userId);
        var role = _rolePinning.EffectiveRole(user);
        return new AccountEntitlements(
            role,
            _plans.For(role),
            _garments.ListGarmentsByUser(userId).Count,
            _outfits.ListOutfitsByUser(userId).Count,
            _bodyPhotos.ListBodyReferencePhotosByUser(userId).Count,
            _credits.GetBalance(user));
    }

    public PlanLimits LimitsFor(string userId)
    {
        return _plans.For(_rolePinning.EffectiveRole(RequireUser(userId)));
    }

    public void EnsureCanAddGarment(string userId)
    {
        var limits = LimitsFor(userId);
        if (limits.MaxGarments is { } max && _garments.ListGarmentsByUser(userId).Count >= max)
        {
            throw new ValidationException($"Your plan allows up to {max} garments. Remove garments or upgrade to Premium for an unlimited wardrobe.");
        }
    }

    public void EnsureCanAddOutfit(string userId)
    {
        var limits = LimitsFor(userId);
        if (limits.MaxOutfits is { } max && _outfits.ListOutfitsByUser(userId).Count >= max)
        {
            throw new ValidationException($"Your plan allows up to {max} saved outfits. Remove outfits or upgrade to Premium for unlimited outfits.");
        }
    }

    public void EnsureCanAddBodyReferencePhoto(string userId)
    {
        var limits = LimitsFor(userId);
        if (limits.MaxBodyReferencePhotos is { } max && _bodyPhotos.ListBodyReferencePhotosByUser(userId).Count >= max)
        {
            throw new ValidationException($"Your plan allows up to {max} body reference photo(s). Delete one or upgrade to Premium.");
        }
    }

    private UserAccount RequireUser(string userId)
    {
        return _users.GetUserById(userId)
            ?? throw new ValidationException("Account was not found.");
    }
}
