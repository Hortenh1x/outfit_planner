using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

// Per-tier limits. Null caps mean unlimited. AllowedAiModes gates only AI modes;
// ClothesOnlyPreview is free and always available. MaxTryOnResolution caps the provider
// output ("1k" under a "4k" configuration reprices credits accordingly).
public sealed record PlanLimits(
    int? MaxGarments,
    int? MaxOutfits,
    int? MaxBodyReferencePhotos,
    int TrialCredits,
    int MonthlyCredits,
    IReadOnlyList<TryOnMode> AllowedAiModes,
    string MaxTryOnResolution,
    bool PriorityQueue);

// Single source of tier entitlements (PAYWALL_MODEL.md): roles stay the coarse switch,
// every limit decision reads this catalog instead of scattering role checks.
public sealed class PlanCatalog
{
    public static readonly IReadOnlyList<TryOnMode> FreeAiModes = new[]
    {
        TryOnMode.SingleGarmentTryOn
    };

    public static readonly IReadOnlyList<TryOnMode> AllAiModes = new[]
    {
        TryOnMode.SingleGarmentTryOn,
        TryOnMode.SequentialOutfitTryOn,
        TryOnMode.ExperimentalCompositeTryOn
    };

    private readonly PlanLimits _free;
    private readonly PlanLimits _premium;
    private readonly PlanLimits _admin;

    public PlanCatalog(PlanLimits free, PlanLimits premium, PlanLimits admin)
    {
        _free = free;
        _premium = premium;
        _admin = admin;
    }

    // Numbers are the PAYWALL_MODEL.md placeholders; the Api layer can override the
    // numeric caps/allowances through Paywall__* configuration.
    public static PlanCatalog Default { get; } = new(
        free: new PlanLimits(
            MaxGarments: 50,
            MaxOutfits: 20,
            MaxBodyReferencePhotos: 1,
            TrialCredits: 8,
            MonthlyCredits: 0,
            AllowedAiModes: FreeAiModes,
            MaxTryOnResolution: "1k",
            PriorityQueue: false),
        premium: new PlanLimits(
            MaxGarments: null,
            MaxOutfits: null,
            MaxBodyReferencePhotos: 5,
            TrialCredits: 0,
            MonthlyCredits: 100,
            AllowedAiModes: AllAiModes,
            MaxTryOnResolution: "4k",
            PriorityQueue: true),
        admin: new PlanLimits(
            MaxGarments: null,
            MaxOutfits: null,
            MaxBodyReferencePhotos: null,
            TrialCredits: 0,
            MonthlyCredits: 0,
            AllowedAiModes: AllAiModes,
            MaxTryOnResolution: "4k",
            PriorityQueue: true));

    public PlanLimits For(UserRole role)
    {
        return role switch
        {
            UserRole.Admin => _admin,
            UserRole.Premium => _premium,
            _ => _free
        };
    }
}
