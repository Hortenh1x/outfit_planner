import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BuilderPage } from './BuilderPage';

function renderBuilder() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <BuilderPage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe('BuilderPage', () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('uploads missing wardrobe pieces directly from builder empty slots', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.includes('/uploads/garment-photo')) {
        return jsonResponse({ url: 'http://localhost:5000/uploads/garments/linen-shirt.png' }, 201);
      }

      if (url.endsWith('/garments') && init?.method === 'POST') {
        return jsonResponse({
          id: 'garment-1',
          name: 'linen shirt',
          category: 'Top',
          bodyZone: 'Torso',
          imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
          thumbnailUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
          tags: []
        }, 201);
      }

      return jsonResponse([]);
    });

    renderBuilder();

    const addTopInput = await screen.findByLabelText(/add a top in wardrobe/i);
    expect(addTopInput).toHaveAttribute('type', 'file');
    expect(screen.getByLabelText(/add a bottom in wardrobe/i)).toHaveAttribute('type', 'file');
    expect(screen.getByLabelText(/add body photo/i)).toHaveAttribute('type', 'file');
    expect(screen.queryByRole('button', { name: /^add$/i })).not.toBeInTheDocument();

    await userEvent.upload(addTopInput, new File(['shirt'], 'linen shirt.png', { type: 'image/png' }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/uploads/garment-photo'), expect.anything());
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/garments$/), expect.objectContaining({ method: 'POST' }));
    });

    const createCall = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/garments') && init?.method === 'POST');
    expect(JSON.parse(createCall?.[1]?.body as string)).toMatchObject({
      name: 'linen shirt',
      category: 'Top',
      imageUrl: 'http://localhost:5000/uploads/garments/linen-shirt.png',
      tags: []
    });
  });

  it('deletes body reference photos from the builder controls', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input);

      if (url.endsWith('/body-reference-photos') && (!init || init.method === undefined)) {
        return jsonResponse([
          { id: 'body-1', imageUrl: 'http://localhost:5000/uploads/body-reference-photos/body.png' }
        ]);
      }

      if (url.endsWith('/body-reference-photos/body-1') && init?.method === 'DELETE') {
        return new Response(null, { status: 204 });
      }

      return jsonResponse([]);
    });

    renderBuilder();

    await userEvent.click(await screen.findByRole('button', { name: /delete body reference 1/i }));

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/body-reference-photos\/body-1$/), expect.objectContaining({ method: 'DELETE' }));
    });
  });

  it('does not show the AI try-on consent checkbox in builder controls', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    renderBuilder();

    expect(await screen.findAllByRole('button', { name: /save outfit/i })).not.toHaveLength(0);
    expect(screen.queryByText(/I consent to AI try-on processing/i)).not.toBeInTheDocument();
  });

  it('renders real animated mode indicators', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => jsonResponse([]));

    const builder = renderBuilder();

    expect(await within(builder.container).findByRole('button', { name: /clothes only/i })).toBeInTheDocument();
    expect(builder.container.querySelector('.mode-toggle .toggle-motion-indicator')).toBeInTheDocument();
  });
});

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}
