import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { UpgradePage } from './UpgradePage';
import type { BillingStatus } from '../api/client';

function renderUpgrade(initialEntry = '/upgrade') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <UpgradePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function mockApi({ role, billing }: { role: 'Free' | 'Premium' | 'Admin'; billing: BillingStatus }) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);

    if (url.includes('/auth/me')) {
      return jsonResponse({
        user: { id: 'user-1', email: 'user@example.com', displayName: 'User', role },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString()
      });
    }

    if (url.endsWith('/billing')) {
      return jsonResponse(billing);
    }

    return jsonResponse({});
  });
}

const enabledBilling: BillingStatus = {
  enabled: true,
  provider: 'stripe',
  subscriptionPriceConfigured: true,
  premiumDisplayPrice: '$9/mo',
  subscription: null,
  topUpPacks: [{ id: 'pack-20', credits: 20, displayPrice: '$5' }],
  portalAvailable: false
};

describe('UpgradePage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('offers Stripe checkout to free accounts when billing is enabled', async () => {
    mockApi({ role: 'Free', billing: enabledBilling });

    renderUpgrade();

    expect(await screen.findByRole('button', { name: /upgrade with stripe/i })).toBeEnabled();
    expect(screen.getByText('$9/mo')).toBeInTheDocument();
    expect(screen.getByText(/unlimited wardrobe and saved outfits/i)).toBeInTheDocument();
    // Top-up packs are a premium feature; free accounts only see the hint.
    expect(screen.queryByRole('button', { name: /^buy$/i })).not.toBeInTheDocument();
  });

  it('shows top-up packs with buy buttons to premium accounts', async () => {
    mockApi({
      role: 'Premium',
      billing: {
        ...enabledBilling,
        subscription: { status: 'active', currentPeriodEnd: '2026-08-01T00:00:00Z', premiumActive: true },
        portalAvailable: true
      }
    });

    renderUpgrade();

    expect(await screen.findByRole('button', { name: /^buy$/i })).toBeEnabled();
    expect(screen.getByText(/20 credits/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /manage subscription/i })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /upgrade with stripe/i })).not.toBeInTheDocument();
  });

  it('degrades to the ask-the-admin notice when billing is disabled', async () => {
    mockApi({
      role: 'Free',
      billing: {
        enabled: false,
        provider: 'disabled',
        subscriptionPriceConfigured: false,
        premiumDisplayPrice: null,
        subscription: null,
        topUpPacks: [],
        portalAvailable: false
      }
    });

    renderUpgrade();

    expect(await screen.findByText(/billing is not configured on this server/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /upgrade with stripe/i })).not.toBeInTheDocument();
  });

  it('shows the success notice after returning from checkout', async () => {
    mockApi({ role: 'Free', billing: enabledBilling });

    renderUpgrade('/upgrade?checkout=success');

    expect(await screen.findByRole('status')).toHaveTextContent(/payment received/i);
  });
});
