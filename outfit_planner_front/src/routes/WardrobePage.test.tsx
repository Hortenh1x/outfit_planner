import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WardrobePage } from './WardrobePage';

function renderWardrobe() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <WardrobePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('WardrobePage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders category choices', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    renderWardrobe();

    const categoryChoices = await screen.findByRole('radiogroup', { name: /garment type/i });
    expect(categoryChoices).toBeInTheDocument();
    expect(within(categoryChoices).getByRole('radio', { name: /dress/i })).toBeInTheDocument();
    expect(within(categoryChoices).getByRole('radio', { name: /shoes/i })).toBeInTheDocument();
  });

  it('deletes garment photos from wardrobe cards', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/garments') && (!init || init.method === undefined)) {
        return jsonResponse([
          {
            id: 'garment-1',
            name: 'linen shirt',
            category: 'Top',
            bodyZone: 'Torso',
            imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
            thumbnailUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
            tags: []
          }
        ]);
      }

      if (url.endsWith('/garments/garment-1') && init?.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }

      return jsonResponse([]);
    });

    renderWardrobe();

    await userEvent.click(await screen.findByRole('button', { name: /delete linen shirt/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/garments\/garment-1$/), expect.objectContaining({ method: 'DELETE' }));
    });
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
