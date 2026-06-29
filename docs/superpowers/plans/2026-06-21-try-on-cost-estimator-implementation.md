# Try-On Cost Estimator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add backend-enforced try-on cost estimation, explicit try-on modes, provider-safe item classification, generation caching, and Builder confirmation before AI generation.

**Architecture:** Keep `TryOnMode` and job metadata in Domain, put cost estimation and confirmation enforcement in Application, and keep provider/network details in Infrastructure. The API exposes an estimate endpoint and requires generation requests to echo the confirmed mode, credits, and cache key from the current server estimate. The frontend uses the API estimate as the source of truth and never computes paid credits locally.

**Tech Stack:** ASP.NET Core Minimal API on .NET 10, DbUp PostgreSQL migrations, React, TypeScript, Vite, TanStack Query, Vitest, Testing Library, generated OpenAPI frontend types.

---

## References

- Spec: `docs/superpowers/specs/2026-06-21-try-on-cost-estimator-design.md`
- Backend try-on service: `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnService.cs`
- Provider port: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/TryOnProvider.cs`
- Builder route: `outfit_planner_front/src/routes/BuilderPage.tsx`
- API client: `outfit_planner_front/src/api/client.ts`
- Current backend test harness: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

## Scope Check

This is one coherent subsystem: try-on estimation and execution. It crosses backend Application/Infrastructure/API and frontend Builder because the backend contract changes and the user-facing confirmation must be wired end to end. Do not refactor unrelated wardrobe, auth, schedule, share, Docker, or visual-system code.

## File Map

Create:

- `outfit_planner_back/src/OutfitPlanner.Domain/TryOnMode.cs`: shared enum for cost modes.
- `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnCostEstimator.cs`: item classification, credit estimate, cache key builder.
- `outfit_planner_back/database/migrations/002_try_on_cost_cache.sql`: idempotent PostgreSQL metadata migration.
- `outfit_planner_front/src/features/tryon/tryOnText.ts`: small UI text helpers for credits and estimates.
- `outfit_planner_front/src/features/tryon/tryOnText.test.ts`: frontend text helper tests.

Modify:

- `outfit_planner_back/src/OutfitPlanner.Domain/Entities.cs`: add `TryOnJob` metadata properties.
- `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/TryOnProvider.cs`: replace provider input with mode-aware request records.
- `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Repositories.cs`: add cache lookup to `ITryOnJobRepository`.
- `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnService.cs`: estimate, enforce confirmation, cache hit, free mode, process mode-aware requests.
- `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/FashnTryOnProvider.cs`: support mode-aware input and send only body try-on items for normal modes.
- `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/HttpTryOnProviders.cs`: add future provider classes and mode-aware JSON request shape.
- `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/MockTryOnProvider.cs`: support mode-aware input.
- `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/InMemoryOutfitStore.cs`: implement cache lookup.
- `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/PostgresOutfitStore.cs`: read/write new job columns and cache lookup.
- `outfit_planner_back/database/schema.sql`: compatibility snapshot for new columns and index.
- `outfit_planner_back/src/OutfitPlanner.Api/Contracts/ApiContracts.cs`: estimate request/response and start request fields.
- `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`: DI, estimate route, updated start route, provider factory names.
- `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`: backend behavior and schema tests.
- `outfit_planner_front/src/api/client.ts`: add `estimateTryOn`, update `startTryOn`.
- `outfit_planner_front/src/api/client.test.ts`: request contract tests.
- `outfit_planner_front/src/api/generated/responseTypes.ts`: add aliases after API generation.
- `outfit_planner_front/src/api/generated/responseTypes.test.ts`: OpenAPI contract smoke checks.
- `outfit_planner_front/src/types.ts`: export `TryOnMode` and `TryOnCostEstimate`.
- `outfit_planner_front/src/routes/BuilderPage.tsx`: mode selector, estimate button path, confirmation UI.
- `outfit_planner_front/src/routes/BuilderPage.test.tsx`: confirmation and excluded-item tests.
- `README.md`: document modes, cache, visual-only behavior.
- `AGENTS.md`: durable context update.

Generated but ignored:

- `outfit_planner_front/.generated/openapi/*.json`
- `outfit_planner_front/src/api/generated/schema.ts`

## Implementation Constants

Use these exact enum values across backend JSON, OpenAPI, generated types, and frontend state:

```csharp
public enum TryOnMode
{
    ClothesOnlyPreview,
    SingleGarmentTryOn,
    SequentialOutfitTryOn,
    ExperimentalCompositeTryOn
}
```

Use these provider names in metadata and cache keys:

```text
MockTryOnProvider
FashnTryOnProvider
CompositeFashnTryOnProvider
SelfHostedCatVtonProvider
GeneralImageEditTryOnProvider
```

Use this cache key string format before hashing:

```text
tryon:v1|body=body:4f0c0f6a7c8f4e4c9a9f0a5d1c2e3b4a|garments=10000000000000000000000000000001,10000000000000000000000000000002|provider=FashnTryOnProvider|mode=SequentialOutfitTryOn|settings=tryon-v1.6:balanced
```

Hash that string with SHA-256 and store lowercase hex.

## Task 1: Domain Mode And Cost Estimator

**Files:**

- Create: `outfit_planner_back/src/OutfitPlanner.Domain/TryOnMode.cs`
- Create: `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnCostEstimator.cs`
- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Add failing estimator tests**

Add these names to the `tests` list near the existing try-on tests:

```csharp
("try-on estimator classifies outfit items and prices modes", TestTryOnCostEstimatorClassifiesAndPricesModes),
("try-on estimator marks unavailable modes", TestTryOnCostEstimatorMarksUnavailableModes),
```

Add these test functions before `TestTryOnConsentRequired`:

```csharp
static void TestTryOnCostEstimatorClassifiesAndPricesModes()
{
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000001"), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/top.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000002"), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/bottom.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000003"), "loafers", GarmentCategory.Shoes, BodyZone.Feet, "https://app.test/shoes.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000004"), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var estimator = new TryOnCostEstimator();

    var sequential = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.SequentialOutfitTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var composite = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.ExperimentalCompositeTryOn,
        "CompositeFashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: true));

    AssertEqual(2, sequential.BodyTryOnItems.Count, "sequential estimate should classify body try-on items.");
    AssertEqual(2, sequential.VisualOnlyItems.Count, "sequential estimate should classify visual-only items.");
    AssertEqual(2, sequential.EstimatedCredits, "sequential estimate should cost one credit per body try-on item.");
    AssertTrue(sequential.IsAvailable, "sequential estimate should be available for multiple body items.");
    AssertTrue(sequential.RequiresAi, "sequential estimate should require AI.");
    AssertTrue(!sequential.RequiresPremiumConfirmation, "sequential estimate should not be premium.");
    AssertEqual(2, sequential.IncludedGarmentIds.Count, "sequential estimate should include only body try-on items.");
    AssertEqual(2, sequential.ExcludedGarmentIds.Count, "sequential estimate should exclude visual-only items.");
    AssertTrue(sequential.CacheKey.Length == 64, "cache key should be a SHA-256 hex string.");

    AssertEqual(1, composite.EstimatedCredits, "composite estimate should cost one credit.");
    AssertEqual(4, composite.IncludedGarmentIds.Count, "composite estimate should include body and visual items.");
    AssertTrue(composite.RequiresPremiumConfirmation, "composite estimate should require premium confirmation.");
    AssertTrue(composite.HasCachedResult, "estimate should carry cache hit status from the caller.");
}

static void TestTryOnCostEstimatorMarksUnavailableModes()
{
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/top.png"),
        new OutfitItem(Guid.NewGuid(), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/bottom.png"),
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var visualOnlyOutfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var estimator = new TryOnCostEstimator();

    var single = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.SingleGarmentTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var visualOnly = estimator.Estimate(visualOnlyOutfit, new TryOnEstimateInput(
        TryOnMode.SequentialOutfitTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var clothesOnly = estimator.Estimate(visualOnlyOutfit, new TryOnEstimateInput(
        TryOnMode.ClothesOnlyPreview,
        "MockTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));

    AssertTrue(!single.IsAvailable, "single mode should reject multiple body try-on items.");
    AssertTrue(single.Summary.Contains("one body garment", StringComparison.OrdinalIgnoreCase), "single mode should explain the shape issue.");
    AssertTrue(!visualOnly.IsAvailable, "paid normal modes should reject visual-only outfits.");
    AssertTrue(visualOnly.Warnings.Any(warning => warning.Contains("ClothesOnlyPreview", StringComparison.Ordinal)), "visual-only estimate should recommend clothes-only mode.");
    AssertTrue(clothesOnly.IsAvailable, "clothes-only mode should be available for visual-only outfits.");
    AssertEqual(0, clothesOnly.EstimatedCredits, "clothes-only mode should be free.");
    AssertTrue(!clothesOnly.RequiresAi, "clothes-only mode should not require AI.");
}
```

Add this helper near `CreateTwoGarmentOutfit`:

```csharp
static Outfit CreateOutfitWithItems(params OutfitItem[] items)
{
    return new Outfit(
        Guid.NewGuid(),
        "user-a",
        "test outfit",
        items,
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow);
}
```

- [ ] **Step 2: Run backend tests and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails because `TryOnCostEstimator`, `TryOnEstimateInput`, and `TryOnMode` do not exist.

- [ ] **Step 3: Add `TryOnMode` enum**

Create `outfit_planner_back/src/OutfitPlanner.Domain/TryOnMode.cs`:

```csharp
namespace OutfitPlanner.Domain;

public enum TryOnMode
{
    ClothesOnlyPreview,
    SingleGarmentTryOn,
    SequentialOutfitTryOn,
    ExperimentalCompositeTryOn
}
```

- [ ] **Step 4: Add estimator implementation**

Create `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnCostEstimator.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record TryOnEstimateInput(
    TryOnMode Mode,
    string ProviderName,
    string BodyReferenceIdentity,
    string SettingsHash,
    bool HasCachedResult);

public sealed record TryOnCostEstimate(
    TryOnMode Mode,
    string ProviderName,
    IReadOnlyList<OutfitItem> BodyTryOnItems,
    IReadOnlyList<OutfitItem> VisualOnlyItems,
    IReadOnlyList<Guid> IncludedGarmentIds,
    IReadOnlyList<Guid> ExcludedGarmentIds,
    int EstimatedCredits,
    bool IsAvailable,
    bool RequiresAi,
    bool RequiresPremiumConfirmation,
    string CacheKey,
    bool HasCachedResult,
    string Summary,
    IReadOnlyList<string> Warnings);

public sealed class TryOnCostEstimator
{
    private static readonly HashSet<GarmentCategory> BodyTryOnCategories = new()
    {
        GarmentCategory.Top,
        GarmentCategory.Bottom,
        GarmentCategory.Dress,
        GarmentCategory.Outerwear
    };

    public TryOnCostEstimate Estimate(Outfit outfit, TryOnEstimateInput input)
    {
        var bodyItems = outfit.Items.Where(item => BodyTryOnCategories.Contains(item.Category)).ToList();
        var visualItems = outfit.Items.Where(item => !BodyTryOnCategories.Contains(item.Category)).ToList();
        var warnings = new List<string>();
        var included = IncludedItems(input.Mode, bodyItems, visualItems);
        var excluded = outfit.Items
            .Where(item => !included.Any(includedItem => includedItem.GarmentId == item.GarmentId))
            .ToList();
        var isAvailable = true;
        var summary = "Ready to estimate try-on generation.";

        if (input.Mode == TryOnMode.SingleGarmentTryOn && bodyItems.Count != 1)
        {
            isAvailable = false;
            summary = "Single garment try-on requires exactly one body garment.";
        }

        if (input.Mode is TryOnMode.SingleGarmentTryOn or TryOnMode.SequentialOutfitTryOn && bodyItems.Count == 0)
        {
            isAvailable = false;
            summary = "Paid try-on requires at least one top, bottom, dress, or outerwear item.";
            warnings.Add("This outfit has only visual-only items. Use ClothesOnlyPreview for a free preview.");
        }

        if (input.Mode == TryOnMode.SequentialOutfitTryOn && bodyItems.Count > 0)
        {
            summary = $"Sequential outfit try-on will use {bodyItems.Count} body garment run(s).";
        }

        if (input.Mode == TryOnMode.ExperimentalCompositeTryOn)
        {
            summary = "Composite premium try-on will send one composed outfit reference to AI.";
        }

        if (visualItems.Count > 0 && input.Mode != TryOnMode.ExperimentalCompositeTryOn)
        {
            warnings.Add("Shoes, bags, accessories, and hats are visual-only and will not be sent to AI in this mode.");
        }

        var credits = input.Mode switch
        {
            TryOnMode.ClothesOnlyPreview => 0,
            TryOnMode.SingleGarmentTryOn => 1,
            TryOnMode.SequentialOutfitTryOn => bodyItems.Count,
            TryOnMode.ExperimentalCompositeTryOn => 1,
            _ => throw new InvalidOperationException($"Unsupported try-on mode {input.Mode}.")
        };
        var cacheKey = BuildCacheKey(input.BodyReferenceIdentity, included.Select(item => item.GarmentId), input.ProviderName, input.Mode, input.SettingsHash);

        return new TryOnCostEstimate(
            input.Mode,
            input.ProviderName,
            bodyItems,
            visualItems,
            included.Select(item => item.GarmentId).OrderBy(id => id).ToArray(),
            excluded.Select(item => item.GarmentId).OrderBy(id => id).ToArray(),
            credits,
            isAvailable,
            input.Mode != TryOnMode.ClothesOnlyPreview,
            input.Mode == TryOnMode.ExperimentalCompositeTryOn,
            cacheKey,
            input.HasCachedResult,
            summary,
            warnings);
    }

    public static string BuildCacheKey(string bodyReferenceIdentity, IEnumerable<Guid> garmentIds, string providerName, TryOnMode mode, string settingsHash)
    {
        var sortedGarments = string.Join(",", garmentIds.OrderBy(id => id).Select(id => id.ToString("N")));
        var raw = $"tryon:v1|body={bodyReferenceIdentity}|garments={sortedGarments}|provider={providerName}|mode={mode}|settings={settingsHash}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<OutfitItem> IncludedItems(TryOnMode mode, IReadOnlyList<OutfitItem> bodyItems, IReadOnlyList<OutfitItem> visualItems)
    {
        return mode switch
        {
            TryOnMode.ClothesOnlyPreview => Array.Empty<OutfitItem>(),
            TryOnMode.SingleGarmentTryOn => bodyItems,
            TryOnMode.SequentialOutfitTryOn => bodyItems,
            TryOnMode.ExperimentalCompositeTryOn => bodyItems.Concat(visualItems).ToList(),
            _ => throw new InvalidOperationException($"Unsupported try-on mode {mode}.")
        };
    }
}
```

- [ ] **Step 5: Run backend tests and verify GREEN for estimator**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: estimator tests pass. Existing try-on tests can still pass because provider and service signatures are unchanged in this task.

- [ ] **Step 6: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Domain\TryOnMode.cs outfit_planner_back\src\OutfitPlanner.Application\Services\TryOnCostEstimator.cs outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs
git commit -m "Add try-on cost estimator"
```

## Task 2: Mode-Aware Provider Port And Adapters

**Files:**

- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/TryOnProvider.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/FashnTryOnProvider.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/HttpTryOnProviders.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/TryOn/MockTryOnProvider.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`
- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Add failing provider adapter coverage**

In `TestProviderAdaptersImplementPort`, replace the `providerTypes` array with:

```csharp
var providerTypes = new[]
{
    typeof(LocalVtonProvider),
    typeof(LocalCatVtonProvider),
    typeof(ReplicateProvider),
    typeof(FalProvider),
    typeof(FashnTryOnProvider),
    typeof(CompositeFashnTryOnProvider),
    typeof(SelfHostedCatVtonProvider),
    typeof(GeneralImageEditTryOnProvider),
    typeof(MockTryOnProvider)
};
```

Add this test name near the existing FASHN provider tests:

```csharp
("fashn provider sends only body try-on items for normal modes", TestFashnProviderSendsOnlyBodyTryOnItems),
```

Add this test function before `TestFashnProviderRequiresApiKey`:

```csharp
static void TestFashnProviderSendsOnlyBodyTryOnItems()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/top.png\"],\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/final.png\"],\"error\":null}");
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/shirt.png"),
        new OutfitItem(Guid.NewGuid(), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/jeans.png"),
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var bodyItems = outfit.Items.Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom).ToArray();
    var visualItems = outfit.Items.Where(item => item.Category == GarmentCategory.Bag).ToArray();
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings("test-key", "tryon-v1.6", "balanced", 2, TimeSpan.Zero));

    var generation = provider.Generate(new TryOnProviderRequest(
        "user-a",
        outfit.Id,
        TryOnMode.SequentialOutfitTryOn,
        "https://app.test/user.jpg",
        bodyItems,
        visualItems,
        new TryOnGenerationSettings("tryon-v1.6", "balanced", "settings-a")));

    AssertEqual("https://cdn.fashn.ai/final.png", generation.OutputImageUrl, "fashn should return the final normal-mode output.");
    AssertEqual(4, handler.Requests.Count, "fashn should run once per body try-on item.");
    AssertTrue(!handler.Requests.Any(request => request.Body.Contains("bag.png", StringComparison.Ordinal)), "normal FASHN runs must not send visual-only items.");
}
```

- [ ] **Step 2: Run backend tests and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails because new provider classes and `TryOnProviderRequest` do not exist.

- [ ] **Step 3: Replace provider port records**

Replace `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/TryOnProvider.cs` with:

```csharp
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record TryOnGeneration(string ProviderJobId, string OutputImageUrl);

public sealed record TryOnGenerationSettings(string ModelName, string Mode, string SettingsHash);

public sealed record TryOnProviderRequest(
    string UserId,
    Guid OutfitId,
    TryOnMode Mode,
    string BodyReferencePhotoUrl,
    IReadOnlyList<OutfitItem> BodyTryOnItems,
    IReadOnlyList<OutfitItem> VisualOnlyItems,
    TryOnGenerationSettings Settings);

public sealed record TryOnProviderCapabilities(
    string ProviderName,
    string ModelName,
    string ProviderMode,
    string SettingsHash,
    IReadOnlySet<TryOnMode> SupportedModes);

public sealed record TryOnOptions(bool SequentialFlowEnabled);

public interface ITryOnProvider
{
    string Name => GetType().Name;

    TryOnProviderCapabilities Capabilities => new(
        Name,
        "default",
        "default",
        "default",
        new HashSet<TryOnMode>
        {
            TryOnMode.ClothesOnlyPreview,
            TryOnMode.SingleGarmentTryOn,
            TryOnMode.SequentialOutfitTryOn,
            TryOnMode.ExperimentalCompositeTryOn
        });

    TryOnGeneration Generate(TryOnProviderRequest request);

    TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)
    {
        var mode = options.SequentialFlowEnabled ? TryOnMode.SequentialOutfitTryOn : TryOnMode.SingleGarmentTryOn;
        var bodyItems = outfit.Items
            .Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom or GarmentCategory.Dress or GarmentCategory.Outerwear)
            .ToArray();
        var visualItems = outfit.Items
            .Where(item => item.Category is GarmentCategory.Shoes or GarmentCategory.Bag or GarmentCategory.Accessory or GarmentCategory.Hat)
            .ToArray();
        return Generate(new TryOnProviderRequest(
            userId,
            outfit.Id,
            mode,
            bodyReferencePhotoUrl,
            bodyItems,
            visualItems,
            new TryOnGenerationSettings(
                Capabilities.ModelName,
                Capabilities.ProviderMode,
                Capabilities.SettingsHash)));
    }
}
```

Keep `TryOnOptions` and the legacy default `Generate` overload temporarily so older service tests compile until Task 4 updates `TryOnService`.

- [ ] **Step 4: Update mock provider**

Replace `MockTryOnProvider.Generate` with:

```csharp
public string Name => nameof(MockTryOnProvider);

public TryOnProviderCapabilities Capabilities => new(
    Name,
    "mock",
    "mock",
    "mock-v1",
    new HashSet<TryOnMode>
    {
        TryOnMode.ClothesOnlyPreview,
        TryOnMode.SingleGarmentTryOn,
        TryOnMode.SequentialOutfitTryOn,
        TryOnMode.ExperimentalCompositeTryOn
    });

public TryOnGeneration Generate(TryOnProviderRequest request)
{
    var providerJobId = $"mock_{Guid.NewGuid():N}";
    var encodedMode = Uri.EscapeDataString(request.Mode.ToString().ToLowerInvariant());
    return new TryOnGeneration(providerJobId, $"/generated/try-on/{request.OutfitId:N}-{encodedMode}.png");
}
```

Remove the old `Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)` method from `MockTryOnProvider`.

- [ ] **Step 5: Update FASHN provider**

Add this property to `FashnTryOnProvider`:

```csharp
public string Name => nameof(FashnTryOnProvider);

public TryOnProviderCapabilities Capabilities => new(
    Name,
    _settings.ModelName,
    _settings.Mode,
    $"{_settings.ModelName}:{_settings.Mode}",
    new HashSet<TryOnMode>
    {
        TryOnMode.SingleGarmentTryOn,
        TryOnMode.SequentialOutfitTryOn
    });
```

Replace the old public `Generate` method with:

```csharp
public TryOnGeneration Generate(TryOnProviderRequest request)
{
    if (string.IsNullOrWhiteSpace(_settings.ApiKey))
    {
        throw new InvalidOperationException("FASHN API key is not configured.");
    }

    if (request.Mode is not (TryOnMode.SingleGarmentTryOn or TryOnMode.SequentialOutfitTryOn))
    {
        throw new InvalidOperationException($"FASHN does not support {request.Mode}.");
    }

    if (request.BodyTryOnItems.Count == 0)
    {
        throw new InvalidOperationException("At least one body garment is required for FASHN try-on.");
    }

    if (request.Mode == TryOnMode.SingleGarmentTryOn && request.BodyTryOnItems.Count != 1)
    {
        throw new InvalidOperationException("Single garment FASHN try-on requires exactly one body garment.");
    }

    return GenerateSequentially(request.BodyReferencePhotoUrl, request.BodyTryOnItems);
}
```

Delete the old multi-garment `TryOnOptions.SequentialFlowEnabled` guard from this provider; mode selection now expresses single versus sequential explicitly.

- [ ] **Step 6: Update JSON provider adapters and add future classes**

In `HttpTryOnProviders.cs`, add these classes after `LocalCatVtonProvider`:

```csharp
public sealed class SelfHostedCatVtonProvider : JsonTryOnProvider
{
    public SelfHostedCatVtonProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "SelfHostedCatVtonProvider")
    {
    }
}

public sealed class CompositeFashnTryOnProvider : JsonTryOnProvider
{
    public CompositeFashnTryOnProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "CompositeFashnTryOnProvider")
    {
    }
}

public sealed class GeneralImageEditTryOnProvider : JsonTryOnProvider
{
    public GeneralImageEditTryOnProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "GeneralImageEditTryOnProvider")
    {
    }
}
```

Add this property and method signature to `JsonTryOnProvider`:

```csharp
public string Name => _providerName;

public TryOnProviderCapabilities Capabilities => new(
    Name,
    _settings.ModelName,
    _providerName,
    $"{_settings.ModelName}:{_providerName}",
    new HashSet<TryOnMode>
    {
        TryOnMode.SingleGarmentTryOn,
        TryOnMode.SequentialOutfitTryOn,
        TryOnMode.ExperimentalCompositeTryOn
    });

public TryOnGeneration Generate(TryOnProviderRequest request)
```

Replace `outfit` and `options` references inside the JSON body with:

```csharp
request.UserId,
request.OutfitId,
request.BodyReferencePhotoUrl,
request.Mode.ToString(),
request.BodyTryOnItems.Select(item => new HttpTryOnGarment(
    item.GarmentId,
    item.Name,
    item.Category.ToString(),
    item.ThumbnailUrl)).ToArray(),
request.VisualOnlyItems.Select(item => new HttpTryOnGarment(
    item.GarmentId,
    item.Name,
    item.Category.ToString(),
    item.ThumbnailUrl)).ToArray()
```

Replace the `HttpTryOnRequest` file record with:

```csharp
file sealed record HttpTryOnRequest(
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("outfit_id")] Guid OutfitId,
    [property: JsonPropertyName("model_image")] string ModelImage,
    [property: JsonPropertyName("try_on_mode")] string TryOnMode,
    [property: JsonPropertyName("body_try_on_items")] IReadOnlyList<HttpTryOnGarment> BodyTryOnItems,
    [property: JsonPropertyName("visual_only_items")] IReadOnlyList<HttpTryOnGarment> VisualOnlyItems);
```

- [ ] **Step 7: Register future provider choices**

In `Program.cs`, add HTTP clients:

```csharp
builder.Services.AddHttpClient("composite-fashn");
builder.Services.AddHttpClient("self-hosted-catvton");
builder.Services.AddHttpClient("general-image-edit");
```

In `CreateTryOnProvider`, add switch arms:

```csharp
"compositefashn" or "composite-fashn" => new CompositeFashnTryOnProvider(
    httpFactory.CreateClient("composite-fashn"),
    HttpProviderSettings(configuration, "CompositeFashn", "https://api.fashn.ai/v1/", "/try-on", requiresApiKey: true)),
"selfhostedcatvton" or "self-hosted-catvton" => new SelfHostedCatVtonProvider(
    httpFactory.CreateClient("self-hosted-catvton"),
    HttpProviderSettings(configuration, "SelfHostedCatVton", "http://localhost:7861/", "/try-on", requiresApiKey: false)),
"generalimageedit" or "general-image-edit" => new GeneralImageEditTryOnProvider(
    httpFactory.CreateClient("general-image-edit"),
    HttpProviderSettings(configuration, "GeneralImageEdit", "https://api.openai.com/v1/", "/images/edits", requiresApiKey: true)),
```

- [ ] **Step 8: Update older provider tests to new request API**

For each existing FASHN test that calls:

```csharp
provider.Generate("user-a", CreateSingleGarmentOutfit(), "https://app.test/user.jpg", new TryOnOptions(false))
```

replace it with:

```csharp
provider.Generate(CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn))
```

For the existing sequential test, use:

```csharp
provider.Generate(CreateProviderRequest(CreateTwoGarmentOutfit(), TryOnMode.SequentialOutfitTryOn))
```

Add this helper near `CreateTwoGarmentOutfit`:

```csharp
static TryOnProviderRequest CreateProviderRequest(Outfit outfit, TryOnMode mode)
{
    var bodyItems = outfit.Items
        .Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom or GarmentCategory.Dress or GarmentCategory.Outerwear)
        .ToArray();
    var visualItems = outfit.Items
        .Where(item => item.Category is GarmentCategory.Shoes or GarmentCategory.Bag or GarmentCategory.Accessory or GarmentCategory.Hat)
        .ToArray();
    return new TryOnProviderRequest(
        outfit.UserId,
        outfit.Id,
        mode,
        "https://app.test/user.jpg",
        bodyItems,
        visualItems,
        new TryOnGenerationSettings("tryon-v1.6", "balanced", "tryon-v1.6:balanced"));
}
```

- [ ] **Step 9: Run backend tests and build**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: backend tests and API build pass after all provider call sites compile against `TryOnProviderRequest`.

- [ ] **Step 10: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Application\Abstractions\TryOnProvider.cs outfit_planner_back\src\OutfitPlanner.Infrastructure\TryOn outfit_planner_back\src\OutfitPlanner.Api\Program.cs outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs
git commit -m "Make try-on providers mode aware"
```

## Task 3: Job Metadata, Repository Cache Contract, And Storage

**Files:**

- Modify: `outfit_planner_back/src/OutfitPlanner.Domain/Entities.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Repositories.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/InMemoryOutfitStore.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/PostgresOutfitStore.cs`
- Create: `outfit_planner_back/database/migrations/002_try_on_cost_cache.sql`
- Modify: `outfit_planner_back/database/schema.sql`
- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Add failing schema/storage tests**

Add this test name near the privacy retention schema test:

```csharp
("try-on storage persists mode cost and cache metadata", TestTryOnStoragePersistsModeCostAndCacheMetadata),
```

Add this test function near other storage tests:

```csharp
static void TestTryOnStoragePersistsModeCostAndCacheMetadata()
{
    var store = new InMemoryOutfitStore();
    var now = DateTimeOffset.UtcNow;
    var cached = new TryOnJob(
        Guid.NewGuid(),
        "user-a",
        Guid.NewGuid(),
        "https://example.com/person.jpg",
        sequentialFlowEnabled: false,
        TryOnStatus.Succeeded,
        "provider-job",
        "https://example.com/output.jpg",
        null,
        now,
        now)
    {
        ProviderName = "FashnTryOnProvider",
        TryOnMode = TryOnMode.SequentialOutfitTryOn,
        ConfirmedCredits = 2,
        CacheKey = "cache-key-a",
        ProviderSettingsHash = "settings-a",
        ServedFromCache = false,
        IsDeleted = false
    };
    var deleted = cached with
    {
        Id = Guid.NewGuid(),
        CacheKey = "cache-key-deleted",
        IsDeleted = true
    };

    store.AddTryOnJob(cached);
    store.AddTryOnJob(deleted);

    var hit = store.FindSucceededTryOnJobByCacheKey("user-a", "cache-key-a");
    var deletedHit = store.FindSucceededTryOnJobByCacheKey("user-a", "cache-key-deleted");

    AssertEqual(cached.Id, hit?.Id, "cache lookup should return the matching succeeded job.");
    AssertEqual(TryOnMode.SequentialOutfitTryOn, hit!.TryOnMode, "job should persist try-on mode.");
    AssertEqual(2, hit.ConfirmedCredits, "job should persist confirmed credits.");
    AssertEqual("settings-a", hit.ProviderSettingsHash, "job should persist provider settings hash.");
    AssertTrue(deletedHit is null, "deleted outputs must not be cache hits.");
}
```

In `TestPostgresSchemaContainsPrivacyStorageAuthAndRetentionFields`, add these columns to the `column` array:

```csharp
"try_on_mode",
"confirmed_credits",
"cache_key",
"served_from_cache",
"source_cached_job_id",
"provider_settings_hash"
```

- [ ] **Step 2: Run backend tests and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails because job metadata properties and repository cache lookup do not exist.

- [ ] **Step 3: Extend `TryOnJob` metadata**

In `Entities.cs`, keep the existing `TryOnJob` primary constructor and add these init properties inside the record body:

```csharp
public TryOnMode TryOnMode { get; init; } = TryOnMode.SequentialOutfitTryOn;
public int ConfirmedCredits { get; init; }
public string? CacheKey { get; init; }
public bool ServedFromCache { get; init; }
public Guid? SourceCachedJobId { get; init; }
public string? ProviderSettingsHash { get; init; }
```

- [ ] **Step 4: Add repository cache contract**

In `ITryOnJobRepository`, add:

```csharp
TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey);
```

- [ ] **Step 5: Implement in-memory cache lookup**

In `InMemoryOutfitStore`, add:

```csharp
public TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey)
{
    lock (_lock)
    {
        return _tryOnJobs.Values
            .Where(job => job.UserId == userId)
            .Where(job => job.CacheKey == cacheKey)
            .Where(job => job.Status == TryOnStatus.Succeeded)
            .Where(job => !job.IsDeleted)
            .Where(job => !string.IsNullOrWhiteSpace(job.OutputImageUrl))
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 6: Add PostgreSQL migration**

Create `outfit_planner_back/database/migrations/002_try_on_cost_cache.sql`:

```sql
alter table try_on_jobs add column if not exists try_on_mode text not null default 'SequentialOutfitTryOn';
alter table try_on_jobs add column if not exists confirmed_credits integer not null default 0;
alter table try_on_jobs add column if not exists cache_key text;
alter table try_on_jobs add column if not exists served_from_cache boolean not null default false;
alter table try_on_jobs add column if not exists source_cached_job_id uuid references try_on_jobs(id) on delete set null;
alter table try_on_jobs add column if not exists provider_settings_hash text;

create index if not exists ix_try_on_jobs_user_cache_succeeded
on try_on_jobs (user_id, cache_key, created_at desc)
where status = 'Succeeded' and output_image_url is not null and is_deleted = false;
```

- [ ] **Step 7: Update `database/schema.sql`**

In the `create table if not exists try_on_jobs` block, add:

```sql
    try_on_mode text not null default 'SequentialOutfitTryOn',
    confirmed_credits integer not null default 0,
    cache_key text,
    served_from_cache boolean not null default false,
    source_cached_job_id uuid references try_on_jobs(id) on delete set null,
    provider_settings_hash text,
```

After the existing `alter table try_on_jobs add column if not exists is_deleted` line, add:

```sql
alter table try_on_jobs add column if not exists try_on_mode text not null default 'SequentialOutfitTryOn';
alter table try_on_jobs add column if not exists confirmed_credits integer not null default 0;
alter table try_on_jobs add column if not exists cache_key text;
alter table try_on_jobs add column if not exists served_from_cache boolean not null default false;
alter table try_on_jobs add column if not exists source_cached_job_id uuid references try_on_jobs(id) on delete set null;
alter table try_on_jobs add column if not exists provider_settings_hash text;
```

Near the other indexes, add:

```sql
create index if not exists ix_try_on_jobs_user_cache_succeeded
on try_on_jobs (user_id, cache_key, created_at desc)
where status = 'Succeeded' and output_image_url is not null and is_deleted = false;
```

- [ ] **Step 8: Update PostgreSQL store SQL**

In every `select` that reads `try_on_jobs`, append these selected columns after `is_deleted`:

```sql
try_on_mode, confirmed_credits, cache_key, served_from_cache, source_cached_job_id, provider_settings_hash
```

In `AddTryOnJob`, append the same columns and parameters to the insert.

In `UpdateTryOnJob`, set:

```sql
try_on_mode = @try_on_mode,
confirmed_credits = @confirmed_credits,
cache_key = @cache_key,
served_from_cache = @served_from_cache,
source_cached_job_id = @source_cached_job_id,
provider_settings_hash = @provider_settings_hash,
```

In `AddTryOnJobParameters`, add:

```csharp
command.Parameters.AddWithValue("try_on_mode", job.TryOnMode.ToString());
command.Parameters.AddWithValue("confirmed_credits", job.ConfirmedCredits);
command.Parameters.AddWithValue("cache_key", DbValue(job.CacheKey));
command.Parameters.AddWithValue("served_from_cache", job.ServedFromCache);
command.Parameters.AddWithValue("source_cached_job_id", DbValue(job.SourceCachedJobId));
command.Parameters.AddWithValue("provider_settings_hash", DbValue(job.ProviderSettingsHash));
```

In `ReadTryOnJob`, set these properties after `IsDeleted`:

```csharp
TryOnMode = reader.IsDBNull(17) ? TryOnMode.SequentialOutfitTryOn : Enum.Parse<TryOnMode>(reader.GetString(17)),
ConfirmedCredits = reader.IsDBNull(18) ? 0 : reader.GetInt32(18),
CacheKey = reader.IsDBNull(19) ? null : reader.GetString(19),
ServedFromCache = !reader.IsDBNull(20) && reader.GetBoolean(20),
SourceCachedJobId = reader.IsDBNull(21) ? null : reader.GetGuid(21),
ProviderSettingsHash = reader.IsDBNull(22) ? null : reader.GetString(22)
```

Add this method:

```csharp
public TryOnJob? FindSucceededTryOnJobByCacheKey(string userId, string cacheKey)
{
    using var command = _dataSource.CreateCommand("""
        select id, user_id, outfit_id, body_reference_photo_url, sequential_flow_enabled, status,
            provider_job_id, output_image_url, error, created_at, updated_at,
            consent_accepted_at, provider_name, provider_request_id, source_body_photo_id, retention_until, is_deleted,
            try_on_mode, confirmed_credits, cache_key, served_from_cache, source_cached_job_id, provider_settings_hash
        from try_on_jobs
        where user_id = @user_id
            and cache_key = @cache_key
            and status = 'Succeeded'
            and output_image_url is not null
            and is_deleted = false
        order by created_at desc
        limit 1
        """);
    command.Parameters.AddWithValue("user_id", userId);
    command.Parameters.AddWithValue("cache_key", cacheKey);

    using var reader = command.ExecuteReader();
    return reader.Read() ? ReadTryOnJob(reader) : null;
}
```

- [ ] **Step 9: Run backend tests and build**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: storage tests pass and API builds.

- [ ] **Step 10: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Domain\Entities.cs outfit_planner_back\src\OutfitPlanner.Application\Abstractions\Repositories.cs outfit_planner_back\src\OutfitPlanner.Infrastructure\Storage outfit_planner_back\database\migrations\002_try_on_cost_cache.sql outfit_planner_back\database\schema.sql outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs
git commit -m "Persist try-on cost cache metadata"
```

## Task 4: TryOnService Confirmation, Free Mode, Cache Hit, And Processing

**Files:**

- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Services/TryOnService.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`
- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Add failing service tests**

Add these names near existing try-on service tests:

```csharp
("try-on service estimates cost before generation", TestTryOnServiceEstimatesCost),
("try-on service enforces confirmed credits and cache key", TestTryOnServiceEnforcesConfirmedCost),
("try-on service returns cache hits without queueing provider work", TestTryOnServiceReturnsCacheHitsWithoutQueueing),
("try-on service completes clothes-only preview without ai", TestTryOnServiceCompletesClothesOnlyWithoutAi),
```

Add these test functions before `TestTryOnServiceQueuesJobsWithoutInlineProviderCall`:

```csharp
static void TestTryOnServiceEstimatesCost()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment(userId, "jeans", GarmentCategory.Bottom));
    var bag = store.CreateGarment(CreateGarment(userId, "bag", GarmentCategory.Bag));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id, bottom.Id, bag.Id });
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), new CountingTryOnProvider(), new TryOnCostEstimator(), new SystemClock());

    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);

    AssertEqual(2, estimate.EstimatedCredits, "estimate should cost one credit per body garment.");
    AssertEqual(2, estimate.BodyTryOnItems.Count, "estimate should classify body try-on items.");
    AssertEqual(1, estimate.VisualOnlyItems.Count, "estimate should classify visual-only items.");
    AssertTrue(estimate.Warnings.Any(warning => warning.Contains("visual-only", StringComparison.OrdinalIgnoreCase)), "estimate should warn about excluded visual-only items.");
}

static void TestTryOnServiceEnforcesConfirmedCost()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment(userId, "jeans", GarmentCategory.Bottom));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id, bottom.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);

    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, confirmedCredits: 1, confirmedCacheKey: estimate.CacheKey).GetAwaiter().GetResult(),
        "confirmed credits must match server estimate");
    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, confirmedCredits: estimate.EstimatedCredits, confirmedCacheKey: "stale-cache-key").GetAwaiter().GetResult(),
        "confirmed cache key must match server estimate");
    AssertEqual(0, provider.Calls, "confirmation mismatch must stop before provider work.");
}

static void TestTryOnServiceReturnsCacheHitsWithoutQueueing()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);
    var cached = new TryOnJob(Guid.NewGuid(), userId, outfit.Id, "https://example.com/person.jpg", false, TryOnStatus.Succeeded, "cached-provider-job", "https://example.com/cached.jpg", null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-5))
    {
        ProviderName = provider.Name,
        TryOnMode = TryOnMode.SingleGarmentTryOn,
        ConfirmedCredits = estimate.EstimatedCredits,
        CacheKey = estimate.CacheKey,
        ProviderSettingsHash = provider.Capabilities.SettingsHash
    };
    store.AddTryOnJob(cached);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    AssertEqual(TryOnStatus.Succeeded, job.Status, "cache hit should return a succeeded job.");
    AssertTrue(job.ServedFromCache, "cache hit job should record cache provenance.");
    AssertEqual(cached.Id, job.SourceCachedJobId, "cache hit should link to source job.");
    AssertEqual("https://example.com/cached.jpg", job.OutputImageUrl, "cache hit should reuse output.");
    AssertEqual(0, queue.Enqueued.Count, "cache hit should not enqueue work.");
    AssertEqual(0, provider.Calls, "cache hit should not call provider.");
}

static void TestTryOnServiceCompletesClothesOnlyWithoutAi()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.ClothesOnlyPreview, "https://example.com/person.jpg", null);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: false, TryOnMode.ClothesOnlyPreview, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    AssertEqual(TryOnStatus.Succeeded, job.Status, "clothes-only preview should complete synchronously.");
    AssertEqual(0, job.ConfirmedCredits, "clothes-only preview should be free.");
    AssertEqual(0, queue.Enqueued.Count, "clothes-only preview should not enqueue provider work.");
    AssertEqual(0, provider.Calls, "clothes-only preview should not call provider.");
}
```

Update `CountingTryOnProvider` to compile with Task 2's port:

```csharp
public TryOnProviderRequest? LastRequest { get; private set; }
public TryOnProviderCapabilities Capabilities => new(
    Name,
    "test-model",
    "test-mode",
    "test-model:test-mode",
    new HashSet<TryOnMode>
    {
        TryOnMode.SingleGarmentTryOn,
        TryOnMode.SequentialOutfitTryOn,
        TryOnMode.ExperimentalCompositeTryOn
    });

public TryOnGeneration Generate(TryOnProviderRequest request)
{
    Calls++;
    LastRequest = request;
    return new TryOnGeneration("test-provider-job", "https://example.com/output.jpg");
}
```

Remove `LastOptions` from `CountingTryOnProvider`, then update existing assertions from `provider.LastOptions?.SequentialFlowEnabled == true` to:

```csharp
provider.LastRequest?.Mode == TryOnMode.SequentialOutfitTryOn
```

- [ ] **Step 2: Run backend tests and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails because `TryOnService` constructor and methods do not match the tests.

- [ ] **Step 3: Update `TryOnService` constructor and estimate method**

Change fields and constructor to:

```csharp
private readonly IBodyReferencePhotoRepository _bodyPhotos;
private readonly IOutfitRepository _outfits;
private readonly ITryOnJobRepository _jobs;
private readonly ITryOnJobQueue _queue;
private readonly ITryOnProvider _provider;
private readonly TryOnCostEstimator _estimator;
private readonly IClock _clock;

public TryOnService(
    IBodyReferencePhotoRepository bodyPhotos,
    IOutfitRepository outfits,
    ITryOnJobRepository jobs,
    ITryOnJobQueue queue,
    ITryOnProvider provider,
    TryOnCostEstimator estimator,
    IClock clock)
{
    _bodyPhotos = bodyPhotos;
    _outfits = outfits;
    _jobs = jobs;
    _queue = queue;
    _provider = provider;
    _estimator = estimator;
    _clock = clock;
}
```

Add:

```csharp
public TryOnCostEstimate Estimate(string userId, Guid outfitId, TryOnMode mode, string bodyReferencePhotoUrl, Guid? sourceBodyPhotoId)
{
    var normalizedUserId = InputGuard.NormalizeUserId(userId);
    var normalizedBodyPhotoUrl = InputGuard.RequireText(bodyReferencePhotoUrl, "Body reference photo URL");
    var outfit = _outfits.GetOutfitByUser(normalizedUserId, outfitId)
        ?? throw new InvalidOperationException("Outfit was not found.");
    var bodyIdentity = BodyReferenceIdentity(normalizedUserId, sourceBodyPhotoId, normalizedBodyPhotoUrl);
    var cacheProbe = _estimator.Estimate(outfit, new TryOnEstimateInput(mode, _provider.Name, bodyIdentity, _provider.Capabilities.SettingsHash, hasCachedResult: false));
    var cached = _jobs.FindSucceededTryOnJobByCacheKey(normalizedUserId, cacheProbe.CacheKey);
    return _estimator.Estimate(outfit, new TryOnEstimateInput(mode, _provider.Name, bodyIdentity, _provider.Capabilities.SettingsHash, cached is not null));
}

private string BodyReferenceIdentity(string userId, Guid? sourceBodyPhotoId, string bodyReferencePhotoUrl)
{
    if (sourceBodyPhotoId is { } photoId)
    {
        var photo = _bodyPhotos.GetBodyReferencePhotoByUser(userId, photoId)
            ?? throw new InvalidOperationException("Body reference photo was not found.");
        return $"body:{photo.Id:N}";
    }

    return $"url:{bodyReferencePhotoUrl.Trim()}";
}
```

- [ ] **Step 4: Update `StartAsync` signature and confirmation enforcement**

Replace the `StartAsync` signature with:

```csharp
public async Task<TryOnJob> StartAsync(
    string userId,
    Guid outfitId,
    string bodyReferencePhotoUrl,
    bool consentAccepted,
    TryOnMode tryOnMode,
    int confirmedCredits,
    string confirmedCacheKey,
    Guid? sourceBodyPhotoId = null,
    CancellationToken cancellationToken = default)
```

At the start of the method, normalize user/body URL, load outfit, estimate, then enforce:

```csharp
var estimate = Estimate(normalizedUserId, outfitId, tryOnMode, normalizedBodyPhotoUrl, sourceBodyPhotoId);
if (!estimate.IsAvailable)
{
    throw new InvalidOperationException(estimate.Summary);
}

if (confirmedCredits != estimate.EstimatedCredits)
{
    throw new InvalidOperationException("Confirmed credits do not match the current try-on estimate. Refresh the estimate before generating.");
}

if (!string.Equals(confirmedCacheKey, estimate.CacheKey, StringComparison.Ordinal))
{
    throw new InvalidOperationException("Confirmed estimate is stale. Refresh the estimate before generating.");
}

if (estimate.RequiresAi && !consentAccepted)
{
    throw new InvalidOperationException("Explicit consent is required before sending photos to an AI provider.");
}

if (!_provider.Capabilities.SupportedModes.Contains(tryOnMode) && estimate.RequiresAi)
{
    throw new InvalidOperationException($"{_provider.Name} does not support {tryOnMode}.");
}
```

Create `TryOnJob` with:

```csharp
TryOnMode = tryOnMode,
ConfirmedCredits = estimate.EstimatedCredits,
CacheKey = estimate.CacheKey,
ProviderSettingsHash = _provider.Capabilities.SettingsHash,
ProviderName = _provider.Name,
ConsentAcceptedAt = estimate.RequiresAi ? now : null,
SourceBodyPhotoId = sourceBodyPhotoId,
RetentionUntil = now.Add(_outputRetention),
IsDeleted = false
```

Before enqueueing, add free/cache paths:

```csharp
if (!estimate.RequiresAi)
{
    var completed = started with
    {
        Status = TryOnStatus.Succeeded,
        UpdatedAt = now
    };
    _jobs.AddTryOnJob(completed);
    return completed;
}

var cached = _jobs.FindSucceededTryOnJobByCacheKey(normalizedUserId, estimate.CacheKey);
if (cached is not null)
{
    var cacheHit = started with
    {
        Status = TryOnStatus.Succeeded,
        ProviderJobId = cached.ProviderJobId,
        ProviderRequestId = cached.ProviderRequestId,
        OutputImageUrl = cached.OutputImageUrl,
        ServedFromCache = true,
        SourceCachedJobId = cached.Id,
        UpdatedAt = now
    };
    _jobs.AddTryOnJob(cacheHit);
    return cacheHit;
}
```

Then add and enqueue the queued job as before.

Keep the old sync `Start` wrapper by changing it to:

```csharp
public TryOnJob Start(string userId, Guid outfitId, string bodyReferencePhotoUrl, bool consentAccepted, bool sequentialFlowEnabled = false)
{
    var mode = sequentialFlowEnabled ? TryOnMode.SequentialOutfitTryOn : TryOnMode.SingleGarmentTryOn;
    var estimate = Estimate(userId, outfitId, mode, bodyReferencePhotoUrl, null);
    return StartAsync(userId, outfitId, bodyReferencePhotoUrl, consentAccepted, mode, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();
}
```

- [ ] **Step 5: Update processing to mode-aware provider request**

In `ProcessQueuedJobAsync`, before calling `_provider.Generate`, recompute the estimate:

```csharp
var estimate = _estimator.Estimate(outfit, new TryOnEstimateInput(
    queued.TryOnMode,
    queued.ProviderName ?? _provider.Name,
    BodyReferenceIdentity(queued.UserId, queued.SourceBodyPhotoId, queued.BodyReferencePhotoUrl),
    queued.ProviderSettingsHash ?? _provider.Capabilities.SettingsHash,
    hasCachedResult: false));
```

Replace the provider call with:

```csharp
var generation = _provider.Generate(new TryOnProviderRequest(
    queued.UserId,
    outfit.Id,
    queued.TryOnMode,
    queued.BodyReferencePhotoUrl,
    estimate.BodyTryOnItems,
    estimate.VisualOnlyItems,
    new TryOnGenerationSettings(
        _provider.Capabilities.ModelName,
        _provider.Capabilities.ProviderMode,
        _provider.Capabilities.SettingsHash)));
```

- [ ] **Step 6: Register estimator and update DI**

In `Program.cs`, add:

```csharp
builder.Services.AddSingleton<TryOnCostEstimator>();
```

The existing `TryOnService` registration stays singleton; DI will now supply `IBodyReferencePhotoRepository` and `TryOnCostEstimator`.

- [ ] **Step 7: Run backend tests and build**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: service tests pass, provider processing uses `TryOnProviderRequest`, and API builds.

- [ ] **Step 8: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Application\Services\TryOnService.cs outfit_planner_back\src\OutfitPlanner.Api\Program.cs outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs
git commit -m "Enforce try-on cost confirmation"
```

## Task 5: API Estimate Contract And OpenAPI Types

**Files:**

- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Contracts/ApiContracts.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Program.cs`
- Modify: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`
- Modify: `outfit_planner_front/src/api/generated/responseTypes.test.ts`
- Modify: `outfit_planner_front/src/api/generated/responseTypes.ts`
- Modify: `outfit_planner_front/src/types.ts`

- [ ] **Step 1: Add failing backend API contract test**

In `TestApiDocumentsFrontendResponseBodies`, add to `requiredMetadata`:

```csharp
".Produces<TryOnEstimateResponse>(StatusCodes.Status200OK)"
```

After the existing `SharedOutfitResponse` assertion, add:

```csharp
AssertTrue(contracts.Contains("public sealed record EstimateTryOnRequest", StringComparison.Ordinal), "estimate try-on request should be a named API contract.");
AssertTrue(contracts.Contains("public sealed record TryOnEstimateResponse", StringComparison.Ordinal), "try-on estimate response should be a named API contract.");
AssertTrue(contracts.Contains("public sealed record TryOnEstimateItemResponse", StringComparison.Ordinal), "try-on estimate items should be named API contracts.");
AssertTrue(contracts.Contains("TryOnMode TryOnMode", StringComparison.Ordinal), "start request should include try-on mode.");
AssertTrue(contracts.Contains("int ConfirmedCredits", StringComparison.Ordinal), "start request should include confirmed credits.");
AssertTrue(contracts.Contains("string ConfirmedCacheKey", StringComparison.Ordinal), "start request should include confirmed cache key.");
```

Add a Program route assertion in `TestApiExposesEditDeleteFilterAndRevokeEndpoints` or a new API string test:

```csharp
AssertTrue(program.Contains("MapPost(\"/outfits/{outfitId:guid}/try-on/estimate\"", StringComparison.Ordinal), "api should expose try-on estimate endpoint.");
```

- [ ] **Step 2: Run backend tests and verify RED**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: fails because the contracts and route do not exist.

- [ ] **Step 3: Update API contracts**

In `ApiContracts.cs`, replace `StartTryOnRequest` with:

```csharp
public sealed record EstimateTryOnRequest(
    string BodyReferencePhotoUrl,
    TryOnMode TryOnMode,
    Guid? BodyReferencePhotoId = null);

public sealed record StartTryOnRequest(
    string BodyReferencePhotoUrl,
    bool ConsentAccepted,
    TryOnMode TryOnMode,
    int ConfirmedCredits,
    string ConfirmedCacheKey,
    Guid? BodyReferencePhotoId = null);

public sealed record TryOnEstimateItemResponse(
    Guid GarmentId,
    string Name,
    GarmentCategory Category,
    BodyZone BodyZone,
    string ThumbnailUrl);

public sealed record TryOnEstimateResponse(
    TryOnMode Mode,
    string Provider,
    IReadOnlyList<TryOnEstimateItemResponse> BodyTryOnItems,
    IReadOnlyList<TryOnEstimateItemResponse> VisualOnlyItems,
    IReadOnlyList<Guid> IncludedGarmentIds,
    IReadOnlyList<Guid> ExcludedGarmentIds,
    int EstimatedCredits,
    bool IsAvailable,
    bool RequiresAi,
    bool RequiresPremiumConfirmation,
    string CacheKey,
    bool HasCachedResult,
    string Summary,
    IReadOnlyList<string> Warnings);
```

- [ ] **Step 4: Add API mapper helper**

In `Program.cs`, add near other local helper methods:

```csharp
static TryOnEstimateResponse ToTryOnEstimateResponse(TryOnCostEstimate estimate)
{
    return new TryOnEstimateResponse(
        estimate.Mode,
        estimate.ProviderName,
        estimate.BodyTryOnItems.Select(ToEstimateItem).ToArray(),
        estimate.VisualOnlyItems.Select(ToEstimateItem).ToArray(),
        estimate.IncludedGarmentIds,
        estimate.ExcludedGarmentIds,
        estimate.EstimatedCredits,
        estimate.IsAvailable,
        estimate.RequiresAi,
        estimate.RequiresPremiumConfirmation,
        estimate.CacheKey,
        estimate.HasCachedResult,
        estimate.Summary,
        estimate.Warnings);
}

static TryOnEstimateItemResponse ToEstimateItem(OutfitItem item)
{
    return new TryOnEstimateItemResponse(
        item.GarmentId,
        item.Name,
        item.Category,
        item.BodyZone,
        item.ThumbnailUrl);
}
```

- [ ] **Step 5: Add estimate route and update start route**

Before the existing `/outfits/{outfitId:guid}/try-on` route, add:

```csharp
api.MapPost("/outfits/{outfitId:guid}/try-on/estimate", (
    Guid outfitId,
    EstimateTryOnRequest request,
    TryOnService tryOn,
    HttpContext context) =>
{
    try
    {
        var estimate = tryOn.Estimate(
            CurrentUser(context),
            outfitId,
            request.TryOnMode,
            request.BodyReferencePhotoUrl,
            request.BodyReferencePhotoId);
        return Results.Ok(ToTryOnEstimateResponse(estimate));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<TryOnEstimateResponse>(StatusCodes.Status200OK);
```

Inside the existing start route, replace the service call arguments with:

```csharp
var job = await tryOn.StartAsync(
    CurrentUser(context),
    outfitId,
    request.BodyReferencePhotoUrl,
    request.ConsentAccepted,
    request.TryOnMode,
    request.ConfirmedCredits,
    request.ConfirmedCacheKey,
    request.BodyReferencePhotoId,
    cancellationToken);
```

- [ ] **Step 6: Run backend tests and build**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: backend API contract tests pass and OpenAPI-capable API builds.

- [ ] **Step 7: Add failing frontend generated-type test**

In `outfit_planner_front/src/api/generated/responseTypes.test.ts`, add schema expectations:

```ts
expect(schema).toContain('"application/json": components["schemas"]["TryOnEstimateResponse"];');
expect(schema).toContain('TryOnMode: "ClothesOnlyPreview" | "SingleGarmentTryOn" | "SequentialOutfitTryOn" | "ExperimentalCompositeTryOn";');
```

- [ ] **Step 8: Generate API and update response aliases**

Run:

```powershell
cd outfit_planner_front
npm run generate:api
cd ..
```

In `responseTypes.ts`, add:

```ts
export type TryOnCostEstimate = JsonResponse<paths['/api/outfits/{outfitId}/try-on/estimate']['post'], 200>;
export type TryOnMode = TryOnCostEstimate['mode'];
```

If `TryOnMode` is generated under `components['schemas']` instead of through the estimate response, use:

```ts
export type TryOnMode = TryOnCostEstimate['mode'];
```

This keeps frontend consumers independent from generated `components` imports.

In `types.ts`, add `TryOnCostEstimate` and `TryOnMode` to the export list.

- [ ] **Step 9: Run frontend generated-type test**

Run:

```powershell
cd outfit_planner_front
npm test -- src/api/generated/responseTypes.test.ts
cd ..
```

Expected: API generation runs first and the generated schema includes the new estimate contract.

- [ ] **Step 10: Commit**

Run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Api\Contracts\ApiContracts.cs outfit_planner_back\src\OutfitPlanner.Api\Program.cs outfit_planner_back\tests\OutfitPlanner.Api.Tests\Program.cs outfit_planner_front\src\api\generated\responseTypes.ts outfit_planner_front\src\api\generated\responseTypes.test.ts outfit_planner_front\src\types.ts
git commit -m "Expose try-on cost estimate API"
```

## Task 6: Frontend API Client Contract

**Files:**

- Modify: `outfit_planner_front/src/api/client.ts`
- Modify: `outfit_planner_front/src/api/client.test.ts`

- [ ] **Step 1: Add failing client test**

In `client.test.ts`, add `estimateTryOn` to the import list.

Add this test before the existing start try-on test:

```ts
it('requests try-on estimates before confirmed generation', async () => {
  const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(
    new Response(JSON.stringify({
      mode: 'SequentialOutfitTryOn',
      provider: 'FashnTryOnProvider',
      bodyTryOnItems: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' }],
      visualOnlyItems: [{ garmentId: 'bag-1', name: 'bag', category: 'Bag', bodyZone: 'Accessory', thumbnailUrl: '/bag.png' }],
      includedGarmentIds: ['top-1'],
      excludedGarmentIds: ['bag-1'],
      estimatedCredits: 1,
      isAvailable: true,
      requiresAi: true,
      requiresPremiumConfirmation: false,
      cacheKey: 'cache-key-a',
      hasCachedResult: false,
      summary: 'Sequential outfit try-on will use 1 body garment run(s).',
      warnings: ['Bags are visual-only.']
    }), {
      status: 200,
      headers: { 'Content-Type': 'application/json' }
    })
  );

  await estimateTryOn({
    outfitId: 'outfit-1',
    bodyReferencePhotoUrl: 'https://example.com/body.jpg',
    bodyReferencePhotoId: 'body-1',
    tryOnMode: 'SequentialOutfitTryOn'
  });

  const [url, init] = fetchMock.mock.calls[0];
  expect(url).toBe('/api/outfits/outfit-1/try-on/estimate');
  expect(init).toMatchObject({ method: 'POST', credentials: 'include' });
  expect(JSON.parse(init?.body as string)).toMatchObject({
    bodyReferencePhotoUrl: 'https://example.com/body.jpg',
    bodyReferencePhotoId: 'body-1',
    tryOnMode: 'SequentialOutfitTryOn'
  });
});
```

Replace the existing start try-on request expectation with:

```ts
await startTryOn({
  outfitId: 'outfit-1',
  bodyReferencePhotoUrl: 'https://example.com/body.jpg',
  bodyReferencePhotoId: 'body-1',
  consentAccepted: true,
  tryOnMode: 'SequentialOutfitTryOn',
  confirmedCredits: 2,
  confirmedCacheKey: 'cache-key-a'
});

expect(JSON.parse(init?.body as string)).toMatchObject({
  bodyReferencePhotoUrl: 'https://example.com/body.jpg',
  bodyReferencePhotoId: 'body-1',
  consentAccepted: true,
  tryOnMode: 'SequentialOutfitTryOn',
  confirmedCredits: 2,
  confirmedCacheKey: 'cache-key-a'
});
```

- [ ] **Step 2: Run frontend client test and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/api/client.test.ts
cd ..
```

Expected: fails because `estimateTryOn` and the new start payload fields do not exist.

- [ ] **Step 3: Update API client imports**

At the top of `client.ts`, add `TryOnCostEstimate` and `TryOnMode` to the type import:

```ts
TryOnCostEstimate,
TryOnMode
```

- [ ] **Step 4: Add `estimateTryOn`**

Before `startTryOn`, add:

```ts
export function estimateTryOn(input: {
  outfitId: string;
  bodyReferencePhotoUrl: string;
  bodyReferencePhotoId?: string;
  tryOnMode: TryOnMode;
}): Promise<TryOnCostEstimate> {
  return request<TryOnCostEstimate>(`/outfits/${input.outfitId}/try-on/estimate`, {
    method: 'POST',
    body: JSON.stringify({
      bodyReferencePhotoUrl: input.bodyReferencePhotoUrl,
      bodyReferencePhotoId: input.bodyReferencePhotoId,
      tryOnMode: input.tryOnMode
    })
  });
}
```

- [ ] **Step 5: Update `startTryOn` input and body**

Replace `startTryOn` signature with:

```ts
export function startTryOn(input: {
  outfitId: string;
  bodyReferencePhotoUrl: string;
  bodyReferencePhotoId?: string;
  consentAccepted: boolean;
  tryOnMode: TryOnMode;
  confirmedCredits: number;
  confirmedCacheKey: string;
}): Promise<TryOnJob> {
```

Replace the request body with:

```ts
body: JSON.stringify({
  bodyReferencePhotoUrl: input.bodyReferencePhotoUrl,
  bodyReferencePhotoId: input.bodyReferencePhotoId,
  consentAccepted: input.consentAccepted,
  tryOnMode: input.tryOnMode,
  confirmedCredits: input.confirmedCredits,
  confirmedCacheKey: input.confirmedCacheKey
})
```

- [ ] **Step 6: Run frontend client tests**

Run:

```powershell
cd outfit_planner_front
npm test -- src/api/client.test.ts
cd ..
```

Expected: client tests pass.

- [ ] **Step 7: Commit**

Run:

```powershell
git add outfit_planner_front\src\api\client.ts outfit_planner_front\src\api\client.test.ts
git commit -m "Confirm try-on generation through API client"
```

## Task 7: Builder Estimate And Confirmation UI

**Files:**

- Create: `outfit_planner_front/src/features/tryon/tryOnText.ts`
- Create: `outfit_planner_front/src/features/tryon/tryOnText.test.ts`
- Modify: `outfit_planner_front/src/routes/BuilderPage.tsx`
- Modify: `outfit_planner_front/src/routes/BuilderPage.test.tsx`

- [ ] **Step 1: Add failing text helper test**

Create `tryOnText.test.ts`:

```ts
import { describe, expect, it } from 'vitest';
import { creditsLabel, modeLabel } from './tryOnText';

describe('try-on text helpers', () => {
  it('formats credit counts and mode labels', () => {
    expect(creditsLabel(0)).toBe('Free');
    expect(creditsLabel(1)).toBe('1 credit');
    expect(creditsLabel(3)).toBe('3 credits');
    expect(modeLabel('ClothesOnlyPreview')).toBe('Clothes only');
    expect(modeLabel('SingleGarmentTryOn')).toBe('Single garment');
    expect(modeLabel('SequentialOutfitTryOn')).toBe('Sequential outfit');
    expect(modeLabel('ExperimentalCompositeTryOn')).toBe('Composite premium');
  });
});
```

- [ ] **Step 2: Run helper test and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/tryon/tryOnText.test.ts
cd ..
```

Expected: fails because helper file does not exist.

- [ ] **Step 3: Implement text helper**

Create `tryOnText.ts`:

```ts
import type { TryOnMode } from '../../types';

export function creditsLabel(credits: number): string {
  if (credits === 0) {
    return 'Free';
  }

  return credits === 1 ? '1 credit' : `${credits} credits`;
}

export function modeLabel(mode: TryOnMode): string {
  switch (mode) {
    case 'ClothesOnlyPreview':
      return 'Clothes only';
    case 'SingleGarmentTryOn':
      return 'Single garment';
    case 'SequentialOutfitTryOn':
      return 'Sequential outfit';
    case 'ExperimentalCompositeTryOn':
      return 'Composite premium';
  }
}
```

- [ ] **Step 4: Run helper test and verify GREEN**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/tryon/tryOnText.test.ts
cd ..
```

Expected: helper test passes.

- [ ] **Step 5: Add failing Builder confirmation test**

In `BuilderPage.test.tsx`, add this test:

```tsx
it('shows server-estimated cost and confirms before starting generation', async () => {
  const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input);

    if (url.endsWith('/garments')) {
      return jsonResponse([
        {
          id: 'top-1',
          userId: 'user-a',
          name: 'white tee',
          category: 'Top',
          bodyZone: 'Torso',
          imageUrl: '/top.png',
          thumbnailUrl: '/top.png',
          tags: [],
          secondaryColors: [],
          season: [],
          occasion: [],
          isFavorite: false,
          isArchived: false,
          laundryStatus: 'clean',
          createdAt: '2026-06-21T12:00:00Z'
        },
        {
          id: 'bag-1',
          userId: 'user-a',
          name: 'leather bag',
          category: 'Bag',
          bodyZone: 'Accessory',
          imageUrl: '/bag.png',
          thumbnailUrl: '/bag.png',
          tags: [],
          secondaryColors: [],
          season: [],
          occasion: [],
          isFavorite: false,
          isArchived: false,
          laundryStatus: 'clean',
          createdAt: '2026-06-21T12:00:00Z'
        }
      ]);
    }

    if (url.endsWith('/body-reference-photos')) {
      return jsonResponse([{ id: 'body-1', imageUrl: 'https://example.com/body.jpg', createdAt: '2026-06-21T12:00:00Z' }]);
    }

    if (url.endsWith('/outfits') && init?.method === 'POST') {
      return jsonResponse({
        id: 'outfit-1',
        name: 'Today',
        items: [
          { garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' },
          { garmentId: 'bag-1', name: 'leather bag', category: 'Bag', bodyZone: 'Accessory', thumbnailUrl: '/bag.png' }
        ],
        tags: [],
        occasion: [],
        isFavorite: false,
        isArchived: false,
        createdAt: '2026-06-21T12:00:00Z'
      }, 201);
    }

    if (url.endsWith('/outfits/outfit-1/try-on/estimate') && init?.method === 'POST') {
      return jsonResponse({
        mode: 'SequentialOutfitTryOn',
        provider: 'FashnTryOnProvider',
        bodyTryOnItems: [{ garmentId: 'top-1', name: 'white tee', category: 'Top', bodyZone: 'Torso', thumbnailUrl: '/top.png' }],
        visualOnlyItems: [{ garmentId: 'bag-1', name: 'leather bag', category: 'Bag', bodyZone: 'Accessory', thumbnailUrl: '/bag.png' }],
        includedGarmentIds: ['top-1'],
        excludedGarmentIds: ['bag-1'],
        estimatedCredits: 1,
        isAvailable: true,
        requiresAi: true,
        requiresPremiumConfirmation: false,
        cacheKey: 'cache-key-a',
        hasCachedResult: false,
        summary: 'Sequential outfit try-on will use 1 body garment run(s).',
        warnings: ['Shoes, bags, accessories, and hats are visual-only and will not be sent to AI in this mode.']
      });
    }

    if (url.endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST') {
      return jsonResponse({ id: 'job-1', status: 'Queued' }, 202);
    }

    if (url.endsWith('/try-on-jobs/job-1')) {
      return jsonResponse({ id: 'job-1', status: 'Queued' });
    }

    return jsonResponse([]);
  });

  renderBuilder();

  await userEvent.click(await screen.findByRole('button', { name: /white tee/i }));
  await userEvent.click(await screen.findByRole('button', { name: /leather bag/i }));
  await userEvent.click(screen.getByRole('button', { name: /generate preview/i }));

  expect(await screen.findByText(/1 credit/i)).toBeInTheDocument();
  expect(screen.getByText(/leather bag/i)).toBeInTheDocument();
  expect(screen.getByText(/visual-only/i)).toBeInTheDocument();
  expect(fetchMock).not.toHaveBeenCalledWith(expect.stringMatching(/\/try-on$/), expect.anything());

  await userEvent.click(screen.getByRole('button', { name: /confirm generation/i }));

  const startCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/outfits/outfit-1/try-on') && init?.method === 'POST');
  expect(startCall).toBeDefined();
  expect(JSON.parse(startCall?.[1]?.body as string)).toMatchObject({
    tryOnMode: 'SequentialOutfitTryOn',
    confirmedCredits: 1,
    confirmedCacheKey: 'cache-key-a'
  });
});
```

- [ ] **Step 6: Run Builder test and verify RED**

Run:

```powershell
cd outfit_planner_front
npm test -- src/routes/BuilderPage.test.tsx
cd ..
```

Expected: fails because Builder starts generation directly and has no estimate confirmation UI.

- [ ] **Step 7: Update Builder imports and state**

In `BuilderPage.tsx`, change the API import to include `estimateTryOn`.

Add type imports:

```ts
import type { GarmentCategory, Outfit, OutfitSelection, PreviewMode, TryOnCostEstimate, TryOnMode } from '../types';
```

Add text helper import:

```ts
import { creditsLabel, modeLabel } from '../features/tryon/tryOnText';
```

Replace `sequentialFlowEnabled` state with:

```ts
const [tryOnMode, setTryOnMode] = useState<TryOnMode>('SequentialOutfitTryOn');
const [pendingEstimate, setPendingEstimate] = useState<TryOnCostEstimate | null>(null);
```

Add estimate mutation before `tryOnMutation`:

```ts
const estimateMutation = useMutation({ mutationFn: estimateTryOn });
```

- [ ] **Step 8: Add mode selector**

Replace the sequential-flow toggle button with:

```tsx
<div className="tryon-mode-selector" role="group" aria-label="Try-on mode">
  {(['ClothesOnlyPreview', 'SingleGarmentTryOn', 'SequentialOutfitTryOn', 'ExperimentalCompositeTryOn'] as TryOnMode[]).map((option) => (
    <button
      key={option}
      type="button"
      className={tryOnMode === option ? 'flow-toggle active' : 'flow-toggle'}
      aria-pressed={tryOnMode === option}
      onClick={() => {
        setTryOnMode(option);
        setPendingEstimate(null);
      }}
    >
      <GitBranch size={16} />
      <span>{modeLabel(option)}</span>
    </button>
  ))}
</div>
```

- [ ] **Step 9: Change generate click to estimate only**

Replace the generate button `onClick` body with:

```ts
const outfit = await ensureOutfit();
const estimate = await estimateMutation.mutateAsync({
  outfitId: outfit.id,
  bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl,
  bodyReferencePhotoId: selectedBodyPhoto.id,
  tryOnMode
});
setPendingEstimate(estimate);
```

Update the generate disabled state to include `estimateMutation.isPending` and the button label to use:

```tsx
{estimateMutation.isPending ? 'Estimating' : 'Generate preview'}
```

- [ ] **Step 10: Add confirmation panel**

After the generate button, render:

```tsx
{pendingEstimate ? (
  <div className="tryon-confirmation">
    <div>
      <small style={headingStyle}>{modeLabel(pendingEstimate.mode)}</small>
      <strong style={headingStyle}>{creditsLabel(pendingEstimate.estimatedCredits)}</strong>
      <p>{pendingEstimate.summary}</p>
    </div>
    {pendingEstimate.hasCachedResult ? <p>Cached result available</p> : null}
    {pendingEstimate.bodyTryOnItems.length > 0 ? (
      <p>Included: {pendingEstimate.bodyTryOnItems.map((item) => item.name).join(', ')}</p>
    ) : null}
    {pendingEstimate.visualOnlyItems.length > 0 ? (
      <p>Visual-only: {pendingEstimate.visualOnlyItems.map((item) => item.name).join(', ')}</p>
    ) : null}
    {pendingEstimate.warnings.map((warning) => (
      <p className="error" key={warning}>{warning}</p>
    ))}
    <button
      type="button"
      className="clay-button primary-action"
      disabled={!pendingEstimate.isAvailable || tryOnMutation.isPending}
      onClick={async () => {
        const outfit = await ensureOutfit();
        await tryOnMutation.mutateAsync({
          outfitId: outfit.id,
          bodyReferencePhotoUrl: selectedBodyPhoto.imageUrl,
          bodyReferencePhotoId: selectedBodyPhoto.id,
          consentAccepted: pendingEstimate.requiresAi,
          tryOnMode: pendingEstimate.mode,
          confirmedCredits: pendingEstimate.estimatedCredits,
          confirmedCacheKey: pendingEstimate.cacheKey
        });
        setPendingEstimate(null);
        setMode('person');
      }}
    >
      <Sparkles size={16} />
      {tryOnMutation.isPending ? 'Generating' : 'Confirm generation'}
    </button>
  </div>
) : null}
```

Add `estimateMutation.error` to the error list.

- [ ] **Step 11: Add minimal CSS**

Append to `outfit_planner_front/src/styles.css`:

```css
.tryon-mode-selector,
.tryon-confirmation {
  display: grid;
  gap: 10px;
}

.tryon-confirmation {
  padding: 12px;
  border: 1px solid color-mix(in srgb, var(--ink) 16%, transparent);
  border-radius: 8px;
  background: color-mix(in srgb, var(--panel) 92%, white);
}

.tryon-confirmation p {
  margin: 0;
}
```

If `var(--ink)` or `var(--panel)` are not defined in the current stylesheet, use the existing text and panel variables found near the top of `styles.css`.

- [ ] **Step 12: Run Builder tests**

Run:

```powershell
cd outfit_planner_front
npm test -- src/features/tryon/tryOnText.test.ts src/routes/BuilderPage.test.tsx
cd ..
```

Expected: helper and Builder tests pass.

- [ ] **Step 13: Commit**

Run:

```powershell
git add outfit_planner_front\src\features\tryon outfit_planner_front\src\routes\BuilderPage.tsx outfit_planner_front\src\routes\BuilderPage.test.tsx outfit_planner_front\src\styles.css
git commit -m "Confirm try-on cost in builder"
```

## Task 8: Documentation And Durable Context

**Files:**

- Modify: `README.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Update README try-on section**

In `README.md`, replace the paragraph that begins `The FASHN provider submits to /run` with:

```markdown
The Builder asks the API for a try-on cost estimate before generation. The API classifies `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items; `Shoes`, `Bag`, `Accessory`, and `Hat` are visual-only and are excluded from normal AI modes.

Try-on modes:

- `ClothesOnlyPreview`: free, no AI provider call.
- `SingleGarmentTryOn`: FASHN `tryon-v1.6`, 1 credit, exactly one body try-on item.
- `SequentialOutfitTryOn`: FASHN `tryon-v1.6` once per body try-on item, one credit per run.
- `ExperimentalCompositeTryOn`: one composed garment reference image, 1 credit, explicitly premium and allowed to include visual-only items.

Generation requests must echo the server-estimated mode, credits, and cache key. The backend recomputes the estimate and rejects stale or mismatched confirmations. Successful generated jobs are cached by body reference, included garment IDs, provider, mode, and provider settings, so repeat requests can reuse existing outputs without calling AI.
```

- [ ] **Step 2: Update provider configuration table**

In the provider environment variable section, add rows:

```markdown
| `TryOn__Provider` | `Mock` | Use `Mock`, `Fashn`, `CompositeFashn`, `LocalVton`, `LocalCatVton`, `SelfHostedCatVton`, `GeneralImageEdit`, `Replicate`, or `Fal`. Unknown values use the mock provider. |
| `TryOn__CompositeFashn__ApiKey` | empty | Required when `TryOn__Provider=CompositeFashn`. |
| `TryOn__SelfHostedCatVton__BaseUrl` | `http://localhost:7861/` | Self-hosted CatVTON endpoint base URL. |
| `TryOn__GeneralImageEdit__ApiKey` | empty | Required when `TryOn__Provider=GeneralImageEdit`. |
```

If an old `TryOn__Provider` row already exists, replace it rather than creating a duplicate.

- [ ] **Step 3: Update AGENTS.md durable context**

In `AGENTS.md`, replace the two current try-on bullets:

```markdown
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`.
- Multi-garment FASHN generation needs the Builder page `Sequential flow` toggle.
```

with:

```markdown
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`; composite and future providers stay behind explicit provider configuration.
- Try-on generation is backend-estimated and backend-confirmed. Modes are `ClothesOnlyPreview` (free), `SingleGarmentTryOn` (1 credit), `SequentialOutfitTryOn` (N body garments = N credits), and `ExperimentalCompositeTryOn` (1 premium composite credit).
- Try-on AI input classification treats `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items. `Shoes`, `Bag`, `Accessory`, and `Hat` are visual-only and must not be sent to AI unless the user explicitly confirms `ExperimentalCompositeTryOn`.
- Try-on jobs cache by body reference, included garment IDs, provider, mode, and provider settings. Cache hits must not enqueue provider work or call AI.
```

- [ ] **Step 4: Run docs-sensitive tests**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: backend string/schema tests still pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add README.md AGENTS.md
git commit -m "Document try-on cost modes"
```

## Task 9: Final Verification

**Files:**

- Verify: full repository.

- [ ] **Step 1: Run backend tests**

Run:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Expected: all backend console tests pass.

- [ ] **Step 2: Build backend**

Run:

```powershell
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Expected: backend API builds with zero errors.

- [ ] **Step 3: Run frontend tests**

Run:

```powershell
cd outfit_planner_front
npm test
cd ..
```

Expected: `npm run generate:api` runs first, generated schema stays ignored, and all Vitest tests pass.

- [ ] **Step 4: Build frontend**

Run:

```powershell
cd outfit_planner_front
npm run build
cd ..
```

Expected: API generation, TypeScript build, and Vite build pass.

- [ ] **Step 5: Check git status**

Run:

```powershell
git status --short
```

Expected: only intentional source, migration, docs, package, and test changes are present. Generated `outfit_planner_front/.generated/` and `outfit_planner_front/src/api/generated/schema.ts` do not appear.

- [ ] **Step 6: Commit final fixes**

If final verification required small fixes, inspect the changed paths and stage only those exact files. For example, if the final fixes touched the service and Builder route, run:

```powershell
git add outfit_planner_back\src\OutfitPlanner.Application\Services\TryOnService.cs outfit_planner_front\src\routes\BuilderPage.tsx
git commit -m "Stabilize try-on cost estimator"
```

## Final Completion Checklist

- [ ] `TryOnCostEstimator` classifies body try-on and visual-only items.
- [ ] API estimate endpoint returns mode, provider, included/excluded items, credits, cache key, cache-hit status, summary, and warnings.
- [ ] Generation rejects stale or mismatched confirmed credits/cache keys.
- [ ] `ClothesOnlyPreview` costs 0 and never queues or calls AI.
- [ ] `SingleGarmentTryOn` costs 1 and requires exactly one body try-on item.
- [ ] `SequentialOutfitTryOn` costs one credit per body try-on item.
- [ ] `ExperimentalCompositeTryOn` costs 1 and is the only mode allowed to include visual-only items for AI.
- [ ] FASHN normal modes send only body try-on items.
- [ ] Cache hits return succeeded jobs without queueing provider work.
- [ ] PostgreSQL migration and `database/schema.sql` include try-on cache metadata.
- [ ] Builder shows estimate details and requires confirmation before generation.
- [ ] Frontend API client sends `tryOnMode`, `confirmedCredits`, and `confirmedCacheKey`.
- [ ] README and `AGENTS.md` describe the new mode/cost/cache behavior.
- [ ] Backend tests, backend build, frontend tests, and frontend build have all run successfully.
