import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ForgotPasswordPage } from './ForgotPasswordPage';
import { ResetPasswordPage } from './ResetPasswordPage';

function renderAt(path: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('password reset pages', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('sends a reset request and shows the generic confirmation', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.endsWith('/auth/password-reset/availability')) {
        return jsonResponse({ available: true });
      }
      if (url.endsWith('/auth/password-reset/request') && init?.method === 'POST') {
        return jsonResponse({ status: 'password-reset-requested' });
      }
      return jsonResponse({}, 404);
    });

    renderAt('/forgot-password');

    await userEvent.type(await screen.findByLabelText(/email/i), 'ada@example.com');
    await userEvent.click(screen.getByRole('button', { name: /send reset link/i }));

    expect(await screen.findByRole('status')).toHaveTextContent(/if an account exists for ada@example.com/i);
    const requestCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/auth/password-reset/request'));
    expect(JSON.parse(requestCall?.[1]?.body as string)).toEqual({ email: 'ada@example.com' });
  });

  it('explains when the server cannot send reset emails', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
      const url = String(input);
      if (url.endsWith('/auth/password-reset/availability')) {
        return jsonResponse({ available: false });
      }
      return jsonResponse({}, 404);
    });

    renderAt('/forgot-password');

    expect(await screen.findByRole('status')).toHaveTextContent(/not enabled on this demo server/i);
    expect(screen.queryByRole('button', { name: /send reset link/i })).not.toBeInTheDocument();
  });

  it('confirms a new password with the token from the link', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);
      if (url.endsWith('/auth/password-reset/confirm') && init?.method === 'POST') {
        return jsonResponse({ status: 'password-reset' });
      }
      return jsonResponse({}, 404);
    });

    renderAt('/reset-password?token=abc123');

    await userEvent.type(screen.getByLabelText(/^new password$/i), 'newpass12');
    await userEvent.type(screen.getByLabelText(/repeat new password/i), 'newpass12');
    await userEvent.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByRole('status')).toHaveTextContent(/your password was changed/i);
    const confirmCall = fetchMock.mock.calls.find(([url]) => String(url).endsWith('/auth/password-reset/confirm'));
    expect(JSON.parse(confirmCall?.[1]?.body as string)).toEqual({ token: 'abc123', password: 'newpass12', repeatPassword: 'newpass12' });
  });

  it('shows the API message for an expired token', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () =>
      jsonResponse({ error: 'Password reset token is invalid or expired.' }, 400));

    renderAt('/reset-password?token=stale');

    await userEvent.type(screen.getByLabelText(/^new password$/i), 'newpass12');
    await userEvent.type(screen.getByLabelText(/repeat new password/i), 'newpass12');
    await userEvent.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByText(/password reset token is invalid or expired\./i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /request a new link/i })).toHaveAttribute('href', '/forgot-password');
  });

  it('refuses to render the form without a token', () => {
    renderAt('/reset-password');

    expect(screen.getByRole('alert')).toHaveTextContent(/incomplete/i);
    expect(screen.queryByRole('button', { name: /set new password/i })).not.toBeInTheDocument();
  });
});
