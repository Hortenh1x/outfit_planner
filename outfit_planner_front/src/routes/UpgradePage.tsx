import { useEffect, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, Coins, CreditCard, Sparkles } from 'lucide-react';
import {
  accountEntitlementsQueryKey,
  billingStatusQueryKey,
  getBillingStatus,
  openBillingPortal,
  startSubscriptionCheckout,
  startTopUpCheckout
} from '../api/client';
import { authSessionQueryKey, useAuthSession } from '../features/auth/authQueries';
import { redirectToCheckout } from '../features/billing/checkoutRedirect';
import { PageHeader } from '../shared/ui/PageHeader';
import '../features/billing/billing.css';

const PREMIUM_FEATURES = [
  'Unlimited wardrobe and saved outfits',
  'All AI try-on modes, including sequential outfits',
  'Up to 4k try-on output resolution',
  '100 AI credits every month (unused credits roll over)',
  'Credit top-up packs',
  'Priority position in the try-on queue'
];

export function UpgradePage() {
  const queryClient = useQueryClient();
  const sessionQuery = useAuthSession();
  const [searchParams] = useSearchParams();
  const checkoutResult = searchParams.get('checkout');
  const refreshedAfterCheckoutRef = useRef(false);

  const billingQuery = useQuery({ queryKey: billingStatusQueryKey, queryFn: getBillingStatus, retry: 1 });

  // Returning from Stripe: the webhook lands within seconds, so refetch the session,
  // entitlements, and billing state once instead of trusting the redirect alone.
  useEffect(() => {
    if (checkoutResult === 'success' && !refreshedAfterCheckoutRef.current) {
      refreshedAfterCheckoutRef.current = true;
      void queryClient.invalidateQueries({ queryKey: authSessionQueryKey });
      void queryClient.invalidateQueries({ queryKey: accountEntitlementsQueryKey });
      void queryClient.invalidateQueries({ queryKey: billingStatusQueryKey });
    }
  }, [checkoutResult, queryClient]);

  const checkoutMutation = useMutation({
    mutationFn: startSubscriptionCheckout,
    onSuccess: ({ url }) => redirectToCheckout(url)
  });
  const topUpMutation = useMutation({
    mutationFn: startTopUpCheckout,
    onSuccess: ({ url }) => redirectToCheckout(url)
  });
  const portalMutation = useMutation({
    mutationFn: openBillingPortal,
    onSuccess: ({ url }) => redirectToCheckout(url)
  });

  const role = sessionQuery.data?.user.role ?? 'Free';
  const billing = billingQuery.data;
  const mutationError = [checkoutMutation.error, topUpMutation.error, portalMutation.error]
    .find((error): error is Error => error instanceof Error);

  return (
    <section className="upgrade-view">
      <PageHeader
        eyebrow="Plan"
        title="Premium"
        text="The organize–compose–plan loop stays free. Premium sells compute: AI try-on generations and an uncapped wardrobe."
      />

      {checkoutResult === 'success' ? (
        <p className="upgrade-checkout-notice" role="status">
          Payment received. Your plan updates within a few seconds — this page refreshes automatically.
        </p>
      ) : null}
      {checkoutResult === 'cancelled' ? (
        <p className="upgrade-checkout-notice" role="status">
          Checkout was cancelled. Nothing was charged.
        </p>
      ) : null}

      {billingQuery.isPending ? (
        <div className="panel-skeleton" aria-label="Loading billing">
          {Array.from({ length: 3 }, (_, index) => <span key={index} />)}
        </div>
      ) : (
        <div className="upgrade-grid">
          <article className="upgrade-plan-card">
            <div className="upgrade-plan-head">
              <h2>Premium</h2>
              <span className="upgrade-plan-price">
                {billing?.premiumDisplayPrice ?? 'Price shown at checkout'}
              </span>
            </div>
            <ul className="upgrade-plan-features">
              {PREMIUM_FEATURES.map((feature) => (
                <li key={feature}>
                  <Check size={14} aria-hidden="true" />
                  <span>{feature}</span>
                </li>
              ))}
            </ul>
            <p className="upgrade-current-plan">
              <Sparkles size={14} aria-hidden="true" />
              <span>Current plan: {role}</span>
            </p>
            {billing?.subscription ? (
              <p className="upgrade-subscription-meta">
                Subscription: {billing.subscription.status}
                {billing.subscription.currentPeriodEnd
                  ? ` · renews/ends ${new Date(billing.subscription.currentPeriodEnd).toLocaleDateString()}`
                  : ''}
              </p>
            ) : null}
            {!billing?.enabled ? (
              <p className="upgrade-disabled-note">
                Billing is not configured on this server. Ask the admin to upgrade your account.
              </p>
            ) : role === 'Admin' ? (
              <p className="upgrade-disabled-note">Admin accounts have unlimited AI credits and need no plan.</p>
            ) : role === 'Free' ? (
              <button
                type="button"
                className="primary-action"
                disabled={!billing.subscriptionPriceConfigured || checkoutMutation.isPending}
                onClick={() => checkoutMutation.mutate()}
              >
                <CreditCard size={16} />
                {checkoutMutation.isPending ? 'Opening checkout' : 'Upgrade with Stripe'}
              </button>
            ) : billing.portalAvailable ? (
              <button
                type="button"
                className="secondary-action"
                disabled={portalMutation.isPending}
                onClick={() => portalMutation.mutate()}
              >
                {portalMutation.isPending ? 'Opening portal' : 'Manage subscription'}
              </button>
            ) : null}
            {billing?.enabled && role === 'Free' && !billing.subscriptionPriceConfigured ? (
              <p className="upgrade-disabled-note">The subscription price is not configured yet.</p>
            ) : null}
          </article>

          <aside className="upgrade-topups" aria-label="Credit top-ups">
            <h3>Credit top-ups</h3>
            {billing?.enabled && role === 'Premium' && billing.topUpPacks.length > 0 ? (
              billing.topUpPacks.map((pack) => (
                <div className="upgrade-pack-row" key={pack.id}>
                  <span className="upgrade-pack-credits">
                    <Coins size={14} aria-hidden="true" />
                    {pack.credits} credits
                  </span>
                  {pack.displayPrice ? <span className="upgrade-pack-price">{pack.displayPrice}</span> : null}
                  <button
                    type="button"
                    className="secondary-action"
                    disabled={topUpMutation.isPending}
                    onClick={() => topUpMutation.mutate(pack.id)}
                  >
                    Buy
                  </button>
                </div>
              ))
            ) : (
              <p className="upgrade-topups-hint">
                {role === 'Premium'
                  ? 'No top-up packs are configured yet.'
                  : 'One-off credit packs that never expire are part of the Premium plan.'}
              </p>
            )}
          </aside>
        </div>
      )}

      {mutationError ? <p className="error" role="alert">{mutationError.message}</p> : null}
    </section>
  );
}
