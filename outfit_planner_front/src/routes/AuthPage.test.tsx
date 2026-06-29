import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AuthPageContent } from './AuthPage';
import type { AuthProvider } from '../api/client';

function renderAuth(returnUrl: string, mode: 'signin' | 'register') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const providers: AuthProvider[] = [
    { id: 'email', label: 'Email', configured: true, flow: 'password' },
    { id: 'google', label: 'Google', configured: false, flow: 'oauth' },
    { id: 'apple', label: 'Apple', configured: false, flow: 'oidc' }
  ];

  vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse({
    user: { id: 'user-a', email: 'ada@example.com', displayName: 'Ada' },
    expiresAt: '2026-07-09T12:00:00Z'
  }));

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/signin']}>
        <Routes>
          <Route path="/signin" element={<AuthPageContent mode={mode} providers={providers} returnUrl={returnUrl} />} />
          <Route path="/register" element={<AuthPageContent mode={mode} providers={providers} returnUrl={returnUrl} />} />
          <Route path="/builder" element={<h1>Builder target</h1>} />
          <Route path="/wardrobe" element={<h1>Wardrobe target</h1>} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('AuthPageContent', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('returns to the requested internal URL after sign in', async () => {
    renderAuth('/wardrobe', 'signin');

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'abc12345');
    await userEvent.click(screen.getByRole('button', { name: /^sign in$/i }));

    expect(await screen.findByRole('heading', { name: /wardrobe target/i })).toBeInTheDocument();
  });

  it('falls back to builder for sanitized return URLs', async () => {
    renderAuth('/builder', 'signin');

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'abc12345');
    await userEvent.click(screen.getByRole('button', { name: /^sign in$/i }));

    expect(await screen.findByRole('heading', { name: /builder target/i })).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
