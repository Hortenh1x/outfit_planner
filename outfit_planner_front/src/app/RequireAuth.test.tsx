import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { RequireAuth } from './RequireAuth';

function renderGuard(initialEntry: string, fetchImpl: typeof fetch) {
  vi.spyOn(globalThis, 'fetch').mockImplementation(fetchImpl);
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false
      }
    }
  });

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route element={<RequireAuth />}>
            <Route path="/builder" element={<h1>Private builder</h1>} />
            <Route path="/wardrobe" element={<h1>Private wardrobe</h1>} />
          </Route>
          <Route path="/signin" element={<SignInProbe />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

function SignInProbe() {
  const location = useLocation();

  return <h1>{`Sign in ${location.search}`}</h1>;
}

describe('RequireAuth', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('shows a skeleton while the session is loading', async () => {
    renderGuard('/builder', () => new Promise<Response>(() => undefined));

    expect(screen.getByLabelText(/loading private page/i)).toBeInTheDocument();
  });

  it('redirects anonymous users to sign in with the current returnUrl', async () => {
    renderGuard('/builder?mode=person', async () => jsonResponse({ error: 'Authentication required' }, 401));

    expect(await screen.findByRole('heading', { name: /returnUrl=%2Fbuilder%3Fmode%3Dperson/i })).toBeInTheDocument();
  });

  it('renders private content for authenticated users', async () => {
    renderGuard('/wardrobe', async () => jsonResponse({
      user: { id: 'user-a', email: 'ada@example.com', displayName: 'Ada' },
      expiresAt: '2026-07-09T12:00:00Z'
    }));

    expect(await screen.findByRole('heading', { name: /private wardrobe/i })).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
