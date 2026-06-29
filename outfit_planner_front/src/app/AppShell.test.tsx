import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

function renderShell() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.endsWith('/auth/me')) {
      return jsonResponse({
        user: { id: 'user-1', email: 'sienna@example.test', displayName: 'Sienna Studio', username: 'Sienna Studio', avatarUrl: null, gender: null },
        expiresAt: '2026-07-20T12:00:00Z'
      });
    }

    if (url.endsWith('/account/profile') && init?.method === 'PATCH') {
      return jsonResponse({
        user: { id: 'user-1', email: 'sienna@example.test', displayName: 'Dmytro Bolibok', username: 'Dmytro Bolibok', avatarUrl: null, gender: 'Male' },
        expiresAt: '2026-07-20T12:00:00Z'
      });
    }

    if (url.endsWith('/auth/logout') && init?.method === 'POST') {
      return jsonResponse({ status: 'signed-out' });
    }

    if (url.endsWith('/auth/providers')) {
      return jsonResponse([]);
    }

    return jsonResponse({});
  });

  const view = render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/wardrobe']}>
        <Routes>
          <Route element={<AppShell />}>
            <Route path="/wardrobe" element={<h1>Wardrobe route</h1>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>
  );

  return { ...view, fetchMock };
}

describe('AppShell editorial frame', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('renders private routes inside the editorial shell without clay ambient blobs', async () => {
    const { container } = renderShell();
    const routeHeading = await screen.findByRole('heading', { name: /wardrobe route/i });
    const editorialShell = container.querySelector('.editorial-shell');

    expect(routeHeading).toBeInTheDocument();
    expect(editorialShell).toBeInTheDocument();
    expect(editorialShell).toContainElement(routeHeading);
    expect(container.querySelector('.editorial-sidebar')).toBeInTheDocument();
    expect(container.querySelector('.clay-ambient')).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: /^primary navigation$/i })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: /^mobile primary navigation$/i })).toBeInTheDocument();
  });

  it('maps the theme toggle to the editorial light and dark themes', async () => {
    const { container } = renderShell();

    expect(container.querySelector('.editorial-shell')).toHaveAttribute('data-theme', 'light');
    await userEvent.click(await screen.findByRole('button', { name: /switch to dark theme/i }));

    expect(container.querySelector('.editorial-shell')).toHaveAttribute('data-theme', 'dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
  });

  it('opens account settings and saves username with gender', async () => {
    const { fetchMock } = renderShell();

    await userEvent.click(await screen.findByRole('button', { name: /sienna studio/i }));
    expect(screen.getByRole('dialog', { name: /account settings/i })).toBeInTheDocument();
    await userEvent.clear(screen.getByLabelText(/username/i));
    await userEvent.type(screen.getByLabelText(/username/i), 'Dmytro Bolibok');
    await userEvent.click(screen.getByRole('button', { name: /^male$/i }));
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => {
      const profileCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/account/profile') && init?.method === 'PATCH');
      expect(profileCall).toBeDefined();
      expect(JSON.parse(profileCall?.[1]?.body as string)).toMatchObject({
        username: 'Dmytro Bolibok',
        gender: 'Male'
      });
    });
    expect(await screen.findAllByText('Dmytro Bolibok')).not.toHaveLength(0);
  });

  it('confirms sign out inside account settings before logging out', async () => {
    const { fetchMock } = renderShell();

    await userEvent.click(await screen.findByRole('button', { name: /sienna studio/i }));
    await userEvent.click(screen.getByRole('button', { name: /sign out/i }));

    expect(screen.getByRole('dialog', { name: /confirm sign out/i })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/auth/logout'))).toBe(false);

    await userEvent.click(screen.getByRole('button', { name: /confirm sign out/i }));

    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/auth/logout') && init?.method === 'POST')).toBe(true);
    });
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
