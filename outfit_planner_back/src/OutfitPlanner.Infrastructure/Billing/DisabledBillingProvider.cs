using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Billing;

// Selected when no billing credentials are configured: status reads as disabled and
// every money-moving operation is rejected with the app's validation surface.
public sealed class DisabledBillingProvider : IBillingProvider
{
    public string Name => "disabled";

    public bool Enabled => false;

    public Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        throw new ValidationException("Billing is not configured.");
    }

    public Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        throw new ValidationException("Billing is not configured.");
    }

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
    {
        throw new ValidationException("Billing is not configured.");
    }

    public BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader)
    {
        return null;
    }
}
