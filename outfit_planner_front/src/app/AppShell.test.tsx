import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppShell } from './AppShell';

function renderShell() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);
    if (url.endsWith('/auth/me')) {
      return jsonResponse({
        user: { id: 'user-1', email: 'sienna@example.test', displayName: 'Sienna Studio' },
        expiresAt: '2026-07-20T12:00:00Z'
      });
    }

    if (url.endsWith('/auth/providers')) {
      return jsonResponse([]);
    }

    return jsonResponse({});
  });

  return render(
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

    expect(await screen.findByRole('heading', { name: /wardrobe route/i })).toBeInTheDocument();
    expect(container.querySelector('.editorial-shell')).toBeInTheDocument();
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
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
