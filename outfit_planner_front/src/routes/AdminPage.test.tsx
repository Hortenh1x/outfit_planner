import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminPage } from './AdminPage';
import { RequireAdmin } from '../app/RequireAdmin';

const pinnedAdminRow = {
  id: 'usr_admin',
  email: 'dmytro.bolibok@gmail.com',
  username: 'Dmytro',
  gender: 'Male',
  role: 'Admin',
  rolePinned: true,
  createdAt: '2026-06-01T10:00:00Z',
  lastLoginAt: '2026-07-06T10:00:00Z',
  emailVerifiedAt: null,
  garmentCount: 4,
  outfitCount: 2,
  tryOnJobCount: 1,
  bodyReferencePhotoCount: 1,
  activeSessionCount: 2,
  avatarUrl: null,
  creditBalance: null
};

const memberRow = {
  id: 'usr_member',
  email: 'member@example.com',
  username: 'Member',
  gender: null,
  role: 'Free',
  rolePinned: false,
  createdAt: '2026-06-20T10:00:00Z',
  lastLoginAt: null,
  emailVerifiedAt: null,
  garmentCount: 1,
  outfitCount: 1,
  tryOnJobCount: 0,
  bodyReferencePhotoCount: 0,
  activeSessionCount: 1,
  avatarUrl: null,
  creditBalance: 6
};

function renderAdminRoute({ sessionRole = 'Admin' }: { sessionRole?: string } = {}) {
  const roleChangeCalls: Array<{ url: string; body: unknown }> = [];
  const creditsCalls: Array<{ url: string; body: unknown }> = [];

  const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? 'GET';

    if (url.includes('/auth/me')) {
      return jsonResponse({
        user: {
          id: 'usr_admin',
          email: 'dmytro.bolibok@gmail.com',
          displayName: 'Dmytro',
          username: 'Dmytro',
          avatarUrl: null,
          gender: 'Male',
          role: sessionRole
        },
        expiresAt: '2026-07-20T12:00:00Z'
      });
    }

    if (url.includes('/admin/stats')) {
      return jsonResponse({ totalUsers: 2, totalGarments: 5, totalOutfits: 3, totalTryOnJobs: 1 });
    }

    if (url.includes('/admin/users/usr_member/role') && method === 'PUT') {
      roleChangeCalls.push({ url, body: init?.body ? JSON.parse(String(init.body)) : null });
      return jsonResponse({ ...memberRow, role: 'Premium' });
    }

    if (url.includes('/admin/users/usr_member/credits') && method === 'POST') {
      creditsCalls.push({ url, body: init?.body ? JSON.parse(String(init.body)) : null });
      return jsonResponse({ balance: 16 });
    }

    if (url.includes('/admin/users')) {
      return jsonResponse({ items: [pinnedAdminRow, memberRow], totalCount: 2, offset: 0, limit: 20 });
    }

    return jsonResponse({});
  });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const view = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/admin']}>
        <Routes>
          <Route path="/builder" element={<h1>Builder home</h1>} />
          <Route element={<RequireAdmin />}>
            <Route path="/admin" element={<AdminPage />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );

  return { ...view, fetchMock, roleChangeCalls, creditsCalls };
}

describe('AdminPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('shows totals and user rows for an admin', async () => {
    renderAdminRoute();

    expect(await screen.findByRole('heading', { name: 'User management' })).toBeInTheDocument();
    expect(await screen.findByText('member@example.com')).toBeInTheDocument();
    expect(screen.getByText('Try-on jobs')).toBeInTheDocument();

    // The pinned account's role cannot be edited, and the pinned marker explains why.
    expect(screen.getByLabelText('Role of Dmytro')).toBeDisabled();
    expect(screen.getByText('Pinned')).toBeInTheDocument();
    expect(screen.getByLabelText('Delete the account of Dmytro')).toBeDisabled();

    // A regular member can be edited and deleted.
    expect(screen.getByLabelText('Role of Member')).toBeEnabled();
    expect(screen.getByLabelText('Delete the account of Member')).toBeEnabled();
  });

  it('changes a member role through the role select', async () => {
    const { roleChangeCalls } = renderAdminRoute();

    const select = await screen.findByLabelText('Role of Member');
    await userEvent.selectOptions(select, 'Premium');

    await waitFor(() => expect(roleChangeCalls).toHaveLength(1));
    expect(roleChangeCalls[0].body).toEqual({ role: 'Premium' });
    expect(await screen.findByText('Member is now Premium.')).toBeInTheDocument();
  });

  it('adjusts a member credit balance from the credits cell', async () => {
    const { creditsCalls } = renderAdminRoute();

    // The admin account bypasses the ledger entirely.
    expect(await screen.findByText('unlimited')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'AI credits of Member: 6. Adjust' }));
    await userEvent.type(screen.getByLabelText('Credit delta'), '10');
    await userEvent.click(screen.getByRole('button', { name: 'Apply' }));

    await waitFor(() => expect(creditsCalls).toHaveLength(1));
    expect(creditsCalls[0].body).toEqual({ delta: 10 });
    expect(await screen.findByText('AI credits of Member: 16.')).toBeInTheDocument();
  });

  it('asks for confirmation before deleting an account', async () => {
    renderAdminRoute();

    await userEvent.click(await screen.findByLabelText('Delete the account of Member'));

    expect(await screen.findByRole('alertdialog', { name: 'Confirm deleting Member' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });

  it('redirects non-admin accounts away from /admin', async () => {
    renderAdminRoute({ sessionRole: 'Free' });

    expect(await screen.findByRole('heading', { name: 'Builder home' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'User management' })).not.toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
