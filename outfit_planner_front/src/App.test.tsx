import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import App from './App';

function renderApp(initialEntry = '/share/token-1') {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false }
    }
  });

  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input) => {
    const url = String(input);

    if (url.includes('/auth/providers')) {
      return jsonResponse([]);
    }

    if (url.includes('/auth/me')) {
      return new Response(null, { status: 401 });
    }

    if (url.includes('/share/token-1')) {
      return jsonResponse({
        id: 'outfit-1',
        name: 'Shared clay',
        items: [],
        tags: [],
        occasion: [],
        isFavorite: false,
        isArchived: false,
        clothesOnlyPreviewUrl: null,
        personPreviewUrl: null,
        createdAt: '2026-06-09T12:00:00Z'
      });
    }

    return jsonResponse([]);
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[initialEntry]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('App shell', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('keeps the compatibility App export and renders public share routes inside the shell', async () => {
    renderApp();

    expect(screen.getByRole('link', { name: /outfit planner/i })).toBeInTheDocument();
    expect(await screen.findByText(/shared clay/i)).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
